using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.Reports;
using PocketFrame.Scenarios;

namespace PocketFrame.Runner;

public sealed class ScenarioRunner
{
    private readonly ScenarioLoader scenarioLoader;
    private readonly ScenarioValidator scenarioValidator;
    private readonly AutomationPipeClient automationClient;
    private readonly MarkdownReportWriter reportWriter;

    public ScenarioRunner()
        : this(new ScenarioLoader(), new ScenarioValidator(), new AutomationPipeClient(), new MarkdownReportWriter())
    {
    }

    public ScenarioRunner(
        ScenarioLoader scenarioLoader,
        ScenarioValidator scenarioValidator,
        AutomationPipeClient automationClient,
        MarkdownReportWriter reportWriter)
    {
        this.scenarioLoader = scenarioLoader;
        this.scenarioValidator = scenarioValidator;
        this.automationClient = automationClient;
        this.reportWriter = reportWriter;
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
        result.Artifacts.ScenarioPath = Path.Combine(context.RunDirectory, "scenario.json");
        result.Artifacts.StatePath = Path.Combine(context.RunDirectory, "state.json");
        result.Artifacts.TracePath = Path.Combine(context.RunDirectory, "action-trace.json");
        result.Artifacts.InitialScreenPath = Path.Combine(context.ScreenshotsDirectory, "initial-screen.png");
        result.Artifacts.InitialDevicePath = Path.Combine(context.ScreenshotsDirectory, "initial-device.png");
        result.Artifacts.ReportPath = Path.Combine(context.RunDirectory, "report.md");

        Directory.CreateDirectory(context.ScreenshotsDirectory);
        await scenarioLoader.SaveAsync(scenario, result.Artifacts.ScenarioPath, cancellationToken);

        var state = await SendAsync<AutomationState>("get_state", null, cancellationToken: cancellationToken);
        await WriteJsonAsync(result.Artifacts.StatePath, state, cancellationToken);
        if (!state.Connected)
        {
            result.Errors.Add("PocketFrame.App is not connected to VNC. Connect the app before running the scenario.");
            result.ExitCode = 2;
            return await FinishReportAsync(result, scenario, state, [], [], options, cancellationToken);
        }

        if (!state.DeviceId.Equals(scenario.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Running app device '{state.DeviceId}' does not match scenario device '{scenario.DeviceId}'.");
            result.ExitCode = 2;
            return await FinishReportAsync(result, scenario, state, [], [], options, cancellationToken);
        }

        await SendAsync<WaitStableFrameResult>(
            "wait_stable_frame",
            new WaitStableFrameParams { QuietMs = options.StableQuietMs, TimeoutMs = options.StableTimeoutMs },
            timeoutMs: options.StableTimeoutMs + 1000,
            cancellationToken: cancellationToken);

        var screen = await SendAsync<CaptureResult>("capture_screen", new CaptureParams { OutputPath = result.Artifacts.InitialScreenPath }, cancellationToken: cancellationToken);
        var device = await SendAsync<CaptureResult>("capture_device", new CaptureParams { OutputPath = result.Artifacts.InitialDevicePath }, cancellationToken: cancellationToken);
        var frame = await SendAsync<FrameHashResult>("frame_hash", null, cancellationToken: cancellationToken);
        var trace = await SendAsync<ActionTraceResult>("action_trace", new ActionTraceParams { OutputPath = result.Artifacts.TracePath }, cancellationToken: cancellationToken);
        var screenshots = new List<ReportScreenshot>
        {
            new() { Label = "Initial screen", Path = RelativeTo(result.Artifacts.InitialScreenPath, context.RunDirectory), FrameIndex = screen.FrameIndex, FrameHash = frame.FrameHash },
            new() { Label = "Initial device", Path = RelativeTo(result.Artifacts.InitialDevicePath, context.RunDirectory), FrameIndex = device.FrameIndex, FrameHash = frame.FrameHash }
        };

        result.Success = true;
        result.ExitCode = 0;
        return await FinishReportAsync(result, scenario, state, trace.Entries, screenshots, options, cancellationToken);
    }

    private async Task<RunResult> FinishReportAsync(
        RunResult result,
        ScenarioDefinition scenario,
        AutomationState state,
        List<AutomationActionTraceEntry> trace,
        List<ReportScreenshot> screenshots,
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
            Screenshots = screenshots,
            Errors = result.Errors
        };
        await reportWriter.WriteAsync(report, result.Artifacts.ReportPath, cancellationToken);
        return result;
    }

    private async Task<T> SendAsync<T>(string method, object? parameters, int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        var response = await automationClient.SendAsync(method, parameters, timeoutMs, cancellationToken);
        if (!response.Ok)
        {
            throw new InvalidOperationException($"{response.Error?.Code}: {response.Error?.Message}");
        }

        return ConvertResult<T>(response.Result);
    }

    private static T ConvertResult<T>(object? result)
    {
        if (result is JsonElement element)
        {
            return element.Deserialize<T>(AutomationJson.Options) ??
                   throw new InvalidOperationException($"Unable to deserialize automation result as {typeof(T).Name}.");
        }

        var json = JsonSerializer.Serialize(result, AutomationJson.Options);
        return JsonSerializer.Deserialize<T>(json, AutomationJson.Options) ??
               throw new InvalidOperationException($"Unable to deserialize automation result as {typeof(T).Name}.");
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
}
