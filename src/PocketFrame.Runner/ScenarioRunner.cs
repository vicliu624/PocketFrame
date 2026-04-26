using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.Environments;
using PocketFrame.Reports;
using PocketFrame.Scenarios;

namespace PocketFrame.Runner;

public sealed class ScenarioRunner
{
    private readonly ScenarioLoader scenarioLoader;
    private readonly ScenarioValidator scenarioValidator;
    private readonly IAutomationClient automationClient;
    private readonly MarkdownReportWriter reportWriter;
    private readonly VisualBaselineComparer visualBaselineComparer;

    public ScenarioRunner()
        : this(new ScenarioLoader(), new ScenarioValidator(), new PipeAutomationClient(), new MarkdownReportWriter(), new VisualBaselineComparer())
    {
    }

    public ScenarioRunner(
        ScenarioLoader scenarioLoader,
        ScenarioValidator scenarioValidator,
        IAutomationClient automationClient,
        MarkdownReportWriter reportWriter,
        VisualBaselineComparer? visualBaselineComparer = null)
    {
        this.scenarioLoader = scenarioLoader;
        this.scenarioValidator = scenarioValidator;
        this.automationClient = automationClient;
        this.reportWriter = reportWriter;
        this.visualBaselineComparer = visualBaselineComparer ?? new VisualBaselineComparer();
    }

    public async Task<RunResult> RunAsync(string scenarioPath, ScenarioRunnerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ScenarioRunnerOptions();
        var result = new RunResult();
        var scenario = await scenarioLoader.LoadAsync(scenarioPath, cancellationToken);
        var validation = scenarioValidator.Validate(scenario);
        if (!validation.IsValid)
        {
            result.Errors.AddRange(validation.Errors);
            result.ExitCode = 2;
            return result;
        }

        var context = CreateContext(scenario, scenarioPath);
        result.Artifacts.RunDirectory = context.RunDirectory;
        result.Artifacts.SourceScenarioPath = context.SourceScenarioPath;
        result.Artifacts.ScenarioPath = Path.Combine(context.RunDirectory, "scenario.json");
        result.Artifacts.StatePath = Path.Combine(context.RunDirectory, "state.json");
        result.Artifacts.EnvironmentStatePath = Path.Combine(context.RunDirectory, "environment-state.json");
        result.Artifacts.AppStatusPath = Path.Combine(context.RunDirectory, "app-status.json");
        result.Artifacts.TracePath = Path.Combine(context.RunDirectory, "action-trace.json");
        result.Artifacts.InitialScreenPath = Path.Combine(context.ScreenshotsDirectory, "initial-screen.png");
        result.Artifacts.InitialDevicePath = Path.Combine(context.ScreenshotsDirectory, "initial-device.png");
        result.Artifacts.FailureScreenPath = Path.Combine(context.ScreenshotsDirectory, "failure-screen.png");
        result.Artifacts.FailureDevicePath = Path.Combine(context.ScreenshotsDirectory, "failure-device.png");
        result.Artifacts.ReportPath = Path.Combine(context.RunDirectory, "report.md");

        Directory.CreateDirectory(context.ScreenshotsDirectory);
        await scenarioLoader.SaveAsync(scenario, result.Artifacts.ScenarioPath, cancellationToken);

        if (options.PrepareEnvironment || scenario.Environment.Prepare)
        {
            await PrepareTargetEnvironmentAsync(scenario, result, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(scenario.App.Id) || !string.IsNullOrWhiteSpace(scenario.App.Command))
        {
            await PrepareScenarioAppAsync(scenario, result, cancellationToken);
        }

        if (options.PrepareEnvironment)
        {
            await PrepareAppAsync(scenario, options, cancellationToken);
        }

        var state = await automationClient.SendAsync<AutomationState>("get_state", cancellationToken: cancellationToken);
        await WriteJsonAsync(result.Artifacts.StatePath, state, cancellationToken);
        if (!state.Connected)
        {
            result.Errors.Add("PocketFrame.App is not connected to VNC. Connect the app before running the scenario.");
            result.ExitCode = 2;
            return await FinishReportAsync(result, scenario, state, [], options, cancellationToken);
        }

        if (!state.DeviceId.Equals(scenario.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Running app device '{state.DeviceId}' does not match scenario device '{scenario.DeviceId}'.");
            result.ExitCode = 2;
            return await FinishReportAsync(result, scenario, state, [], options, cancellationToken);
        }

        await automationClient.SendAsync<WaitStableFrameResult>(
            "wait_stable_frame",
            new WaitStableFrameParams { QuietMs = options.StableQuietMs, TimeoutMs = options.StableTimeoutMs },
            timeoutMs: options.StableTimeoutMs + 1000,
            cancellationToken: cancellationToken);

        await CaptureInitialArtifactsAsync(result, context, cancellationToken);

        foreach (var action in scenario.Actions)
        {
            result.Actions.Add(await ExecuteActionAsync(action, result, context, cancellationToken));
        }

        await ExecuteEnvironmentCommandsAsync(scenario.Environment.PostCommands, scenario.Environment, result, cancellationToken);
        if (scenario.App.CleanupOnFinish)
        {
            await automationClient.SendAsync<EnvironmentOperationResult>("environment_app_kill", ToEnvironmentAppParams(scenario), cancellationToken: cancellationToken);
        }

        result.Assertions.AddRange(await EvaluateAssertionsAsync(scenario.Assertions, result, context, options, cancellationToken));
        if (scenario.Run.CaptureOnFailure && HasFailure(result))
        {
            await CaptureFailureArtifactsAsync(result, context, cancellationToken);
        }

        var trace = await automationClient.SendAsync<ActionTraceResult>("action_trace", new ActionTraceParams { OutputPath = result.Artifacts.TracePath }, cancellationToken: cancellationToken);
        result.Success = result.Errors.Count == 0 && AllUnexpectedActionsSucceeded(scenario.Assertions, result.Actions) && result.Assertions.All(assertion => assertion.Passed);
        result.ExitCode = result.Success ? 0 : 1;
        return await FinishReportAsync(result, scenario, state, trace.Entries, options, cancellationToken);
    }

    private async Task CaptureInitialArtifactsAsync(RunResult result, RunContext context, CancellationToken cancellationToken)
    {
        var screen = await automationClient.SendAsync<CaptureResult>("capture_screen", new CaptureParams { OutputPath = result.Artifacts.InitialScreenPath }, cancellationToken: cancellationToken);
        var device = await automationClient.SendAsync<CaptureResult>("capture_device", new CaptureParams { OutputPath = result.Artifacts.InitialDevicePath }, cancellationToken: cancellationToken);
        var frame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
        result.Screenshots.Add(new ReportScreenshot
        {
            Label = "initial-screen",
            Path = RelativeTo(result.Artifacts.InitialScreenPath, context.RunDirectory),
            FrameIndex = screen.FrameIndex,
            FrameHash = frame.FrameHash
        });
        result.Screenshots.Add(new ReportScreenshot
        {
            Label = "initial-device",
            Path = RelativeTo(result.Artifacts.InitialDevicePath, context.RunDirectory),
            FrameIndex = device.FrameIndex,
            FrameHash = frame.FrameHash
        });
    }

    private async Task PrepareTargetEnvironmentAsync(ScenarioDefinition scenario, RunResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scenario.Environment.ProfileId))
        {
            return;
        }

        var state = await automationClient.SendAsync<EnvironmentStateResult>(
            "environment_get_state",
            new EnvironmentCommandParams { ProfileId = scenario.Environment.ProfileId },
            cancellationToken: cancellationToken);
        await WriteJsonAsync(result.Artifacts.EnvironmentStatePath, state, cancellationToken);

        await ExecuteEnvironmentCommandsAsync(scenario.Environment.PreCommands, scenario.Environment, result, cancellationToken);
        if (scenario.Environment.RestartVnc)
        {
            var restart = await automationClient.SendAsync<EnvironmentOperationResult>(
                "environment_restart_vnc",
                ToEnvironmentVncParams(scenario.Environment),
                timeoutMs: 61000,
                cancellationToken: cancellationToken);
            AddEnvironmentOperation(result, restart);
        }
        else if (scenario.Environment.StartVnc)
        {
            var start = await automationClient.SendAsync<EnvironmentOperationResult>(
                "environment_start_vnc",
                ToEnvironmentVncParams(scenario.Environment),
                timeoutMs: 31000,
                cancellationToken: cancellationToken);
            AddEnvironmentOperation(result, start);
        }
    }

    private async Task ExecuteEnvironmentCommandsAsync(
        IReadOnlyList<ScenarioEnvironmentCommand> commands,
        ScenarioEnvironment environment,
        RunResult result,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            try
            {
                var commandResult = await automationClient.SendAsync<EnvironmentCommandResult>(
                    "environment_exec",
                    new EnvironmentCommandParams
                    {
                        ProfileId = environment.ProfileId,
                        Command = command.Command,
                        WorkingDirectory = string.IsNullOrWhiteSpace(command.WorkingDirectory) ? environment.WorkingDirectory : command.WorkingDirectory,
                        TimeoutMs = command.TimeoutMs
                    },
                    timeoutMs: (command.TimeoutMs <= 0 ? 30000 : command.TimeoutMs) + 1000,
                    cancellationToken: cancellationToken);
                result.EnvironmentCommands.Add(commandResult);
                if (commandResult.ExitCode != 0 && !command.ContinueOnFailure)
                {
                    result.Errors.Add($"Environment command failed: {command.Id} exit={commandResult.ExitCode}");
                    break;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Environment command failed: {command.Id} {ex.Message}");
                if (!command.ContinueOnFailure)
                {
                    break;
                }
            }
        }
    }

    private async Task PrepareAppAsync(ScenarioDefinition scenario, ScenarioRunnerOptions options, CancellationToken cancellationToken)
    {
        await automationClient.SendAsync<AutomationOperationResult>(
            "select_device",
            new SelectDeviceParams { DeviceId = scenario.DeviceId },
            cancellationToken: cancellationToken);
        await automationClient.SendAsync<AutomationOperationResult>(
            "set_scale",
            new SetScaleParams { Scale = scenario.Scale <= 0 ? 1 : scenario.Scale },
            cancellationToken: cancellationToken);
        await automationClient.SendAsync<AutomationOperationResult>(
            "connect_vnc",
            new ConnectVncParams
            {
                Host = scenario.Connection.Host,
                Port = scenario.Connection.Port,
                Password = scenario.Connection.Password,
                DeviceId = scenario.DeviceId,
                Scale = scenario.Scale <= 0 ? 1 : scenario.Scale
            },
            timeoutMs: options.StableTimeoutMs + 30000,
            cancellationToken: cancellationToken);
    }

    private static EnvironmentVncParams ToEnvironmentVncParams(ScenarioEnvironment environment) => new()
    {
        ProfileId = environment.ProfileId,
        Display = environment.VncDisplay,
        Geometry = environment.VncGeometry,
        Depth = environment.VncDepth,
        TimeoutMs = 30000
    };

    private async Task PrepareScenarioAppAsync(ScenarioDefinition scenario, RunResult result, CancellationToken cancellationToken)
    {
        var parameters = ToEnvironmentAppParams(scenario);
        if (scenario.App.ClearPaths.Count > 0)
        {
            var clean = await automationClient.SendAsync<EnvironmentOperationResult>("environment_app_clean_state", parameters, cancellationToken: cancellationToken);
            AddEnvironmentOperation(result, clean);
        }

        if (scenario.App.KillBeforeLaunch)
        {
            var kill = await automationClient.SendAsync<EnvironmentOperationResult>("environment_app_kill", parameters, cancellationToken: cancellationToken);
            AddEnvironmentOperation(result, kill);
        }

        if (!string.IsNullOrWhiteSpace(scenario.App.Command))
        {
            var launchParameters = ToEnvironmentAppParams(scenario);
            launchParameters.KillBeforeLaunch = false;
            launchParameters.ClearPaths = [];
            var launch = await automationClient.SendAsync<EnvironmentLaunchResult>("environment_app_launch", launchParameters, cancellationToken: cancellationToken);
            result.EnvironmentCommands.Add(new EnvironmentCommandResult { ProfileId = launch.ProfileId, Command = launch.Command, WorkingDirectory = launch.WorkingDirectory, ExitCode = 0 });
        }

        var status = await automationClient.SendAsync<EnvironmentAppStatusResult>("environment_app_status", parameters, cancellationToken: cancellationToken);
        result.AppStatus = status;
        await WriteJsonAsync(result.Artifacts.AppStatusPath, status, cancellationToken);
    }

    private static EnvironmentAppParams ToEnvironmentAppParams(ScenarioDefinition scenario) => new()
    {
        ProfileId = scenario.Environment.ProfileId,
        Id = scenario.App.Id,
        ProcessMatch = scenario.App.ProcessMatch,
        BinaryPath = scenario.App.BinaryPath,
        Command = scenario.App.Command,
        WorkingDirectory = scenario.App.WorkingDirectory,
        LogPath = scenario.App.LogPath,
        ClearPaths = scenario.App.ClearPaths,
        Env = scenario.App.Env,
        KillBeforeLaunch = scenario.App.KillBeforeLaunch
    };

    private static void AddEnvironmentOperation(RunResult result, EnvironmentOperationResult operation)
    {
        if (operation.Command is not null)
        {
            result.EnvironmentCommands.Add(operation.Command);
        }

        if (!operation.Ok)
        {
            result.Errors.Add(operation.Message);
        }
    }

    private async Task<ScenarioActionResult> ExecuteActionAsync(ScenarioAction action, RunResult runResult, RunContext context, CancellationToken cancellationToken)
    {
        var id = ResolveActionId(action, runResult.Actions.Count);
        var before = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
        var result = new ScenarioActionResult
        {
            Id = id,
            Type = action.Type,
            Label = string.IsNullOrWhiteSpace(action.Label) ? id : action.Label,
            FrameIndexBefore = before.FrameIndex,
            FrameHashBefore = before.FrameHash
        };

        try
        {
            await ExecuteActionBodyAsync(action, id, result, runResult, context, cancellationToken);
            var after = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
            result.FrameIndexAfter = after.FrameIndex;
            result.FrameHashAfter = after.FrameHash;
            result.Ok = true;
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
            if (ShouldCaptureOnFailure(action, context.Scenario))
            {
                await CaptureFailureArtifactsAsync(runResult, context, cancellationToken);
            }
        }

        return result;
    }

    private async Task ExecuteActionBodyAsync(ScenarioAction action, string id, ScenarioActionResult result, RunResult runResult, RunContext context, CancellationToken cancellationToken)
    {
        switch (Normalize(action.Type))
        {
            case "wait":
            case "delay":
                await automationClient.SendAsync<WaitResult>(
                    "wait",
                    new WaitParams { DurationMs = action.DurationMs },
                    timeoutMs: Math.Max(5000, action.DurationMs + 1000),
                    cancellationToken: cancellationToken);
                break;
            case "waitframechange":
                await automationClient.SendAsync<WaitFrameResult>(
                    "wait_frame_change",
                    new WaitFrameChangeParams { AfterFrame = action.AfterFrame, TimeoutMs = action.TimeoutMs },
                    timeoutMs: action.TimeoutMs + 1000,
                    cancellationToken: cancellationToken);
                break;
            case "waitstableframe":
                await automationClient.SendAsync<WaitStableFrameResult>(
                    "wait_stable_frame",
                    new WaitStableFrameParams { QuietMs = action.QuietMs, TimeoutMs = action.TimeoutMs },
                    timeoutMs: action.TimeoutMs + 1000,
                    cancellationToken: cancellationToken);
                break;
            case "capturescreen":
                await CaptureActionAsync("capture_screen", "screen", action, id, result, runResult, context, cancellationToken);
                break;
            case "capturedevice":
                await CaptureActionAsync("capture_device", "device", action, id, result, runResult, context, cancellationToken);
                break;
            case "presskey":
                await automationClient.SendAsync<object>("press_key", new KeyPressParams { Key = action.Key }, cancellationToken: cancellationToken);
                break;
            case "pressbutton":
                await automationClient.SendAsync<object>(
                    "press_button",
                    new ButtonPressParams { ButtonId = action.ButtonId, DurationMs = action.DurationMs },
                    timeoutMs: Math.Max(5000, action.DurationMs + 1000),
                    cancellationToken: cancellationToken);
                break;
            case "clickscreen":
                await automationClient.SendAsync<object>("click_screen", new ClickScreenParams { X = action.X, Y = action.Y, Button = action.Button }, cancellationToken: cancellationToken);
                break;
            case "typetext":
                await automationClient.SendAsync<object>("type_text", new TextInputParams { Text = action.Text }, cancellationToken: cancellationToken);
                break;
            case "savetrace":
                await automationClient.SendAsync<ActionTraceResult>("action_trace", new ActionTraceParams { OutputPath = runResult.Artifacts.TracePath }, cancellationToken: cancellationToken);
                result.ArtifactPath = RelativeTo(runResult.Artifacts.TracePath, context.RunDirectory);
                break;
            default:
                throw new InvalidOperationException($"Unsupported scenario action type: {action.Type}");
        }
    }

    private async Task CaptureActionAsync(
        string method,
        string suffix,
        ScenarioAction action,
        string id,
        ScenarioActionResult actionResult,
        RunResult runResult,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var label = string.IsNullOrWhiteSpace(action.Label) ? id : action.Label;
        var path = Path.Combine(context.ScreenshotsDirectory, $"{SafeFileName(label)}-{suffix}.png");
        var capture = await automationClient.SendAsync<CaptureResult>(method, new CaptureParams { OutputPath = path }, cancellationToken: cancellationToken);
        var frame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
        var relative = RelativeTo(path, context.RunDirectory);
        actionResult.ArtifactPath = relative;
        runResult.Screenshots.Add(new ReportScreenshot
        {
            Label = label,
            Path = relative,
            FrameIndex = capture.FrameIndex,
            FrameHash = frame.FrameHash
        });
    }

    private async Task<List<ScenarioAssertionResult>> EvaluateAssertionsAsync(
        IReadOnlyList<ScenarioAssertion> assertions,
        RunResult runResult,
        RunContext context,
        ScenarioRunnerOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<ScenarioAssertionResult>();
        foreach (var assertion in assertions)
        {
            results.Add(await EvaluateAssertionAsync(assertion, runResult, context, options, results.Count, cancellationToken));
        }

        return results;
    }

    private async Task<ScenarioAssertionResult> EvaluateAssertionAsync(
        ScenarioAssertion assertion,
        RunResult runResult,
        RunContext context,
        ScenarioRunnerOptions options,
        int index,
        CancellationToken cancellationToken)
    {
        var result = new ScenarioAssertionResult
        {
            Id = string.IsNullOrWhiteSpace(assertion.Id) ? $"assertion-{index + 1:000}" : assertion.Id,
            Type = assertion.Type
        };

        switch (Normalize(assertion.Type))
        {
            case "framechanged":
                var action = runResult.Actions.FirstOrDefault(item => item.Id.Equals(assertion.AfterAction, StringComparison.OrdinalIgnoreCase));
                result.Expected = "frame hash changes";
                result.Actual = action is null ? "action not found" : $"{action.FrameHashBefore} -> {action.FrameHashAfter}";
                result.Passed = action is not null &&
                                !string.IsNullOrWhiteSpace(action.FrameHashBefore) &&
                                !string.IsNullOrWhiteSpace(action.FrameHashAfter) &&
                                !action.FrameHashBefore.Equals(action.FrameHashAfter, StringComparison.Ordinal);
                break;
            case "screenshotexists":
                var screenshot = runResult.Screenshots.FirstOrDefault(item => item.Label.Equals(assertion.Label, StringComparison.OrdinalIgnoreCase));
                result.Expected = $"screenshot label '{assertion.Label}' exists";
                result.Actual = screenshot?.Path ?? "missing";
                result.Passed = screenshot is not null && File.Exists(Path.Combine(runResult.Artifacts.RunDirectory, screenshot.Path));
                break;
            case "framehashnotempty":
                var frame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
                result.Expected = "non-empty frame hash";
                result.Actual = frame.FrameHash;
                result.Passed = !string.IsNullOrWhiteSpace(frame.FrameHash);
                break;
            case "framehashequals":
                var equalsFrame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
                result.Expected = assertion.ExpectedHash;
                result.Actual = equalsFrame.FrameHash;
                result.Passed = equalsFrame.FrameHash.Equals(assertion.ExpectedHash, StringComparison.Ordinal);
                break;
            case "framehashnotequals":
                var notEqualsFrame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
                result.Expected = $"not {assertion.ExpectedHash}";
                result.Actual = notEqualsFrame.FrameHash;
                result.Passed = !notEqualsFrame.FrameHash.Equals(assertion.ExpectedHash, StringComparison.Ordinal);
                break;
            case "actionsucceeded":
                var succeededAction = runResult.Actions.FirstOrDefault(item => item.Id.Equals(assertion.AfterAction, StringComparison.OrdinalIgnoreCase));
                result.Expected = $"action '{assertion.AfterAction}' succeeds";
                result.Actual = succeededAction is null ? "action not found" : succeededAction.Ok ? "succeeded" : $"failed: {succeededAction.Error}";
                result.Passed = succeededAction?.Ok == true;
                break;
            case "actionfailed":
                var failedAction = runResult.Actions.FirstOrDefault(item => item.Id.Equals(assertion.AfterAction, StringComparison.OrdinalIgnoreCase));
                result.Expected = $"action '{assertion.AfterAction}' fails";
                result.Actual = failedAction is null ? "action not found" : failedAction.Ok ? "succeeded" : $"failed: {failedAction.Error}";
                result.Passed = failedAction is { Ok: false };
                break;
            case "allactionssucceeded":
                var failedActions = runResult.Actions.Where(item => !item.Ok).Select(item => item.Id).ToList();
                result.Expected = "all actions succeed";
                result.Actual = failedActions.Count == 0 ? "all actions succeeded" : string.Join(", ", failedActions);
                result.Passed = failedActions.Count == 0;
                break;
            case "screenshotmatchesbaseline":
                await EvaluateScreenshotBaselineAssertionAsync(assertion, runResult, result, options, cancellationToken);
                break;
            case "logcontains":
                var logContent = await ReadAssertionTextAsync(assertion, runResult, context, cancellationToken);
                result.Expected = $"text contains '{assertion.Text}'";
                result.Actual = logContent.Contains(assertion.Text, StringComparison.Ordinal) ? assertion.Text : "not found";
                result.Passed = logContent.Contains(assertion.Text, StringComparison.Ordinal);
                break;
            case "jsonequals":
                var jsonContent = await ReadAssertionTextAsync(assertion, runResult, context, cancellationToken);
                result.Expected = assertion.Expected;
                result.Actual = ReadJsonSelector(jsonContent, assertion.Selector);
                result.Passed = string.Equals(result.Actual, assertion.Expected, StringComparison.Ordinal);
                break;
            default:
                result.Expected = "supported assertion type";
                result.Actual = assertion.Type;
                result.Passed = false;
                break;
        }

        if (!result.Passed)
        {
            if (string.IsNullOrWhiteSpace(result.Message))
            {
                result.Message = $"Assertion {result.Id} failed.";
            }

            runResult.Errors.Add(result.Message);
        }

        return result;
    }

    private async Task<string> ReadAssertionTextAsync(ScenarioAssertion assertion, RunResult runResult, RunContext context, CancellationToken cancellationToken)
    {
        var path = assertion.Path;
        if (string.IsNullOrWhiteSpace(path) && assertion.Type.Equals("logContains", StringComparison.OrdinalIgnoreCase))
        {
            path = runResult.AppStatus?.LogPath ?? string.Empty;
        }

        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }

        var runRelativePath = Path.Combine(runResult.Artifacts.RunDirectory, path);
        if (File.Exists(runRelativePath))
        {
            return await File.ReadAllTextAsync(runRelativePath, cancellationToken);
        }

        var scenarioDirectory = Path.GetDirectoryName(Path.GetFullPath(context.SourceScenarioPath));
        if (!string.IsNullOrWhiteSpace(scenarioDirectory))
        {
            var scenarioRelativePath = Path.Combine(scenarioDirectory, path);
            if (File.Exists(scenarioRelativePath))
            {
                return await File.ReadAllTextAsync(scenarioRelativePath, cancellationToken);
            }
        }

        var file = await automationClient.SendAsync<EnvironmentFileResult>(
            "environment_read_file",
            new EnvironmentFileParams { ProfileId = context.Scenario.Environment.ProfileId, Path = path },
            cancellationToken: cancellationToken);
        return file.Content;
    }

    private static string ReadJsonSelector(string json, string selector)
    {
        using var document = JsonDocument.Parse(json);
        var current = document.RootElement;
        foreach (var segment in selector.Trim().TrimStart('$').TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return string.Empty;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : current.ToString();
    }

    private async Task EvaluateScreenshotBaselineAssertionAsync(
        ScenarioAssertion assertion,
        RunResult runResult,
        ScenarioAssertionResult result,
        ScenarioRunnerOptions options,
        CancellationToken cancellationToken)
    {
        var screenshot = runResult.Screenshots.FirstOrDefault(item => item.Label.Equals(assertion.Label, StringComparison.OrdinalIgnoreCase));
        if (screenshot is null)
        {
            result.Expected = $"screenshot label '{assertion.Label}' matches baseline";
            result.Actual = "screenshot not found";
            result.Passed = false;
            return;
        }

        var actualPath = Path.Combine(runResult.Artifacts.RunDirectory, screenshot.Path);
        var baselinePath = ResolveScenarioRelativePath(runResult, assertion.Baseline);
        var diffPath = Path.Combine(runResult.Artifacts.RunDirectory, "screenshots", $"{SafeFileName(result.Id)}-diff.png");
        result.Expected = $"changed ratio <= {assertion.Threshold:0.####}";
        result.BaselinePath = RelativeTo(baselinePath, runResult.Artifacts.RunDirectory);
        result.ActualPath = screenshot.Path;
        result.DiffPath = RelativeTo(diffPath, runResult.Artifacts.RunDirectory);
        result.Threshold = assertion.Threshold;
        result.PixelTolerance = assertion.PixelTolerance;

        if (!File.Exists(actualPath))
        {
            result.Actual = $"actual missing: {screenshot.Path}";
            result.Passed = false;
            return;
        }

        if (!File.Exists(baselinePath))
        {
            if (options.UpdateBaselines)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(baselinePath))!);
                File.Copy(actualPath, baselinePath, overwrite: true);
            }
            else
            {
                result.Actual = $"baseline missing: {assertion.Baseline}";
                result.Passed = false;
                return;
            }
        }

        if (options.UpdateBaselines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(baselinePath))!);
            File.Copy(actualPath, baselinePath, overwrite: true);
        }

        var comparison = await visualBaselineComparer.CompareAsync(
            actualPath,
            baselinePath,
            diffPath,
            new VisualBaselineCompareOptions
            {
                Threshold = assertion.Threshold,
                PixelTolerance = assertion.PixelTolerance,
                Regions = assertion.Regions.Select(ToVisualRegion).ToList(),
                IgnoreRegions = assertion.IgnoreRegions.Select(ToVisualRegion).ToList(),
                MaskRegions = assertion.MaskRegions.Select(ToVisualRegion).ToList()
            },
            cancellationToken);
        result.Passed = comparison.Passed;
        result.Actual = options.UpdateBaselines
            ? $"baseline updated, changed ratio {comparison.ChangedRatio:0.####}"
            : $"changed ratio {comparison.ChangedRatio:0.####}";
        result.Message = options.UpdateBaselines ? "Baseline updated." : comparison.Passed ? string.Empty : comparison.Message;
        result.ChangedPixels = comparison.ChangedPixels;
        result.TotalPixels = comparison.TotalPixels;
        result.IgnoredPixels = comparison.IgnoredPixels;
        result.ChangedRatio = comparison.ChangedRatio;
        result.Threshold = comparison.Threshold;
        result.PixelTolerance = comparison.PixelTolerance;
        if (string.IsNullOrWhiteSpace(comparison.DiffPath))
        {
            result.DiffPath = string.Empty;
        }
    }

    private async Task CaptureFailureArtifactsAsync(RunResult result, RunContext context, CancellationToken cancellationToken)
    {
        if (result.Screenshots.Any(item => item.Label.Equals("failure-screen", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var screen = await automationClient.SendAsync<CaptureResult>("capture_screen", new CaptureParams { OutputPath = result.Artifacts.FailureScreenPath }, cancellationToken: cancellationToken);
        var device = await automationClient.SendAsync<CaptureResult>("capture_device", new CaptureParams { OutputPath = result.Artifacts.FailureDevicePath }, cancellationToken: cancellationToken);
        var frame = await automationClient.SendAsync<FrameHashResult>("frame_hash", cancellationToken: cancellationToken);
        result.Screenshots.Add(new ReportScreenshot
        {
            Label = "failure-screen",
            Path = RelativeTo(result.Artifacts.FailureScreenPath, context.RunDirectory),
            FrameIndex = screen.FrameIndex,
            FrameHash = frame.FrameHash
        });
        result.Screenshots.Add(new ReportScreenshot
        {
            Label = "failure-device",
            Path = RelativeTo(result.Artifacts.FailureDevicePath, context.RunDirectory),
            FrameIndex = device.FrameIndex,
            FrameHash = frame.FrameHash
        });
    }

    private async Task<RunResult> FinishReportAsync(
        RunResult result,
        ScenarioDefinition scenario,
        AutomationState state,
        List<AutomationActionTraceEntry> trace,
        ScenarioRunnerOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.GenerateReport)
        {
            return result;
        }

        var report = new AutomationRunReport
        {
            RunId = Path.GetFileName(result.Artifacts.RunDirectory),
            RunDirectory = result.Artifacts.RunDirectory,
            Scenario = scenario,
            State = state,
            Trace = trace,
            Screenshots = result.Screenshots,
            Errors = result.Errors,
            Success = result.Success,
            ActionResults = result.Actions.Select(ToReportActionResult).ToList(),
            AssertionResults = result.Assertions.Select(ToReportAssertionResult).ToList()
            ,
            EnvironmentCommands = result.EnvironmentCommands
        };
        await reportWriter.WriteAsync(report, result.Artifacts.ReportPath, cancellationToken);
        return result;
    }

    private static RunContext CreateContext(ScenarioDefinition scenario, string sourceScenarioPath)
    {
        var root = string.IsNullOrWhiteSpace(scenario.Run.WorkingDir)
            ? Path.Combine("runs", scenario.Name)
            : scenario.Run.WorkingDir;
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var runDirectory = Path.GetFullPath(Path.Combine(root, timestamp));
        return new RunContext
        {
            Scenario = scenario,
            SourceScenarioPath = sourceScenarioPath,
            RunDirectory = runDirectory
        };
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, AutomationJson.Options, cancellationToken);
    }

    private static string RelativeTo(string path, string relativeTo)
    {
        return Path.GetRelativePath(relativeTo, path).Replace('\\', '/');
    }

    private static string ResolveScenarioRelativePath(RunResult result, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var sourceScenarioPath = string.IsNullOrWhiteSpace(result.Artifacts.SourceScenarioPath)
            ? result.Artifacts.ScenarioPath
            : result.Artifacts.SourceScenarioPath;
        var scenarioDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceScenarioPath)) ?? result.Artifacts.RunDirectory;
        return Path.GetFullPath(Path.Combine(scenarioDirectory, path));
    }

    private static bool HasFailure(RunResult result) =>
        result.Errors.Count > 0 || result.Actions.Any(action => !action.Ok) || result.Assertions.Any(assertion => !assertion.Passed);

    private static bool AllUnexpectedActionsSucceeded(IReadOnlyList<ScenarioAssertion> assertions, IReadOnlyList<ScenarioActionResult> actions)
    {
        var expectedFailures = assertions
            .Where(assertion => Normalize(assertion.Type) == "actionfailed")
            .Select(assertion => assertion.AfterAction)
            .Where(afterAction => !string.IsNullOrWhiteSpace(afterAction))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return actions.All(action => action.Ok || expectedFailures.Contains(action.Id));
    }

    private static bool ShouldCaptureOnFailure(ScenarioAction action, ScenarioDefinition scenario) =>
        action.CaptureOnFailure ?? scenario.Run.CaptureOnFailure;

    private static VisualRegion ToVisualRegion(ScenarioRegion region) =>
        new(region.X, region.Y, region.Width, region.Height);

    private static string ResolveActionId(ScenarioAction action, int index) =>
        string.IsNullOrWhiteSpace(action.Id) ? $"action-{index + 1:000}" : action.Id;

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join('-', value.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static ReportActionResult ToReportActionResult(ScenarioActionResult action) => new()
    {
        Id = action.Id,
        Type = action.Type,
        Label = action.Label,
        Ok = action.Ok,
        Error = action.Error,
        FrameIndexBefore = action.FrameIndexBefore,
        FrameIndexAfter = action.FrameIndexAfter,
        FrameHashBefore = action.FrameHashBefore,
        FrameHashAfter = action.FrameHashAfter,
        ArtifactPath = action.ArtifactPath
    };

    private static ReportAssertionResult ToReportAssertionResult(ScenarioAssertionResult assertion) => new()
    {
        Id = assertion.Id,
        Type = assertion.Type,
        Passed = assertion.Passed,
        Expected = assertion.Expected,
        Actual = assertion.Actual,
        Message = assertion.Message,
        BaselinePath = assertion.BaselinePath,
        ActualPath = assertion.ActualPath,
        DiffPath = assertion.DiffPath,
        ChangedPixels = assertion.ChangedPixels,
        TotalPixels = assertion.TotalPixels,
        IgnoredPixels = assertion.IgnoredPixels,
        ChangedRatio = assertion.ChangedRatio,
        Threshold = assertion.Threshold,
        PixelTolerance = assertion.PixelTolerance
    };
}
