using System.Text.Json;
using PocketFrame.Automation;
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
        result.Artifacts.TracePath = Path.Combine(context.RunDirectory, "action-trace.json");
        result.Artifacts.InitialScreenPath = Path.Combine(context.ScreenshotsDirectory, "initial-screen.png");
        result.Artifacts.InitialDevicePath = Path.Combine(context.ScreenshotsDirectory, "initial-device.png");
        result.Artifacts.FailureScreenPath = Path.Combine(context.ScreenshotsDirectory, "failure-screen.png");
        result.Artifacts.FailureDevicePath = Path.Combine(context.ScreenshotsDirectory, "failure-device.png");
        result.Artifacts.ReportPath = Path.Combine(context.RunDirectory, "report.md");

        Directory.CreateDirectory(context.ScreenshotsDirectory);
        await scenarioLoader.SaveAsync(scenario, result.Artifacts.ScenarioPath, cancellationToken);

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

        result.Assertions.AddRange(await EvaluateAssertionsAsync(scenario.Assertions, result, cancellationToken));
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
                await automationClient.SendAsync<object>("press_button", new ButtonPressParams { ButtonId = action.ButtonId }, cancellationToken: cancellationToken);
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

    private async Task<List<ScenarioAssertionResult>> EvaluateAssertionsAsync(IReadOnlyList<ScenarioAssertion> assertions, RunResult runResult, CancellationToken cancellationToken)
    {
        var results = new List<ScenarioAssertionResult>();
        foreach (var assertion in assertions)
        {
            results.Add(await EvaluateAssertionAsync(assertion, runResult, results.Count, cancellationToken));
        }

        return results;
    }

    private async Task<ScenarioAssertionResult> EvaluateAssertionAsync(ScenarioAssertion assertion, RunResult runResult, int index, CancellationToken cancellationToken)
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
                await EvaluateScreenshotBaselineAssertionAsync(assertion, runResult, result, cancellationToken);
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

    private async Task EvaluateScreenshotBaselineAssertionAsync(
        ScenarioAssertion assertion,
        RunResult runResult,
        ScenarioAssertionResult result,
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

        if (!File.Exists(actualPath))
        {
            result.Actual = $"actual missing: {screenshot.Path}";
            result.Passed = false;
            return;
        }

        if (!File.Exists(baselinePath))
        {
            result.Actual = $"baseline missing: {assertion.Baseline}";
            result.Passed = false;
            return;
        }

        var comparison = await visualBaselineComparer.CompareAsync(actualPath, baselinePath, diffPath, assertion.Threshold, cancellationToken);
        result.Passed = comparison.Passed;
        result.Actual = $"changed ratio {comparison.ChangedRatio:0.####}";
        result.Message = comparison.Passed ? string.Empty : comparison.Message;
        result.ChangedPixels = comparison.ChangedPixels;
        result.TotalPixels = comparison.TotalPixels;
        result.ChangedRatio = comparison.ChangedRatio;
        result.Threshold = comparison.Threshold;
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
        ChangedRatio = assertion.ChangedRatio,
        Threshold = assertion.Threshold
    };
}
