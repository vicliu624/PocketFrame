using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.Reports;
using PocketFrame.Runner;
using PocketFrame.Scenarios;
using Xunit;

namespace PocketFrame.Runner.Tests;

public sealed class ScenarioRunnerTests
{
    [Fact]
    public async Task RunFailsWhenAppIsNotConnected()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "not-connected",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = false, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Errors, error => error.Contains("not connected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunFailsOnDeviceMismatch()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "mismatch",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "uconsole" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Errors, error => error.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunExecutesActionsAssertionsAndWritesArtifacts()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "smoke",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") },
            Actions =
            {
                new ScenarioAction { Id = "type-ls", Type = "typeText", Text = "ls\n" },
                new ScenarioAction { Id = "capture-after-ls", Type = "captureScreen", Label = "after-ls" },
                new ScenarioAction { Id = "save-trace", Type = "saveTrace" }
            },
            Assertions =
            {
                new ScenarioAssertion { Id = "screen-exists", Type = "screenshotExists", Label = "after-ls" },
                new ScenarioAssertion { Id = "hash-present", Type = "frameHashNotEmpty" }
            }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(result.Artifacts.ScenarioPath));
        Assert.True(File.Exists(result.Artifacts.StatePath));
        Assert.True(File.Exists(result.Artifacts.ReportPath));
        Assert.True(File.Exists(Path.Combine(result.Artifacts.RunDirectory, "screenshots", "after-ls-screen.png")));
        Assert.Contains(client.Methods, method => method == "type_text");
        Assert.All(result.Assertions, assertion => Assert.True(assertion.Passed));
    }

    [Fact]
    public async Task RunFailsWhenAssertionFailsAndCapturesFailureArtifacts()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "failure-capture",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs"), CaptureOnFailure = true },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") },
            Actions =
            {
                new ScenarioAction { Id = "type-ls", Type = "typeText", Text = "ls\n" }
            },
            Assertions =
            {
                new ScenarioAssertion { Id = "hash-wrong", Type = "frameHashEquals", ExpectedHash = "not-the-current-hash" }
            }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.True(File.Exists(result.Artifacts.FailureScreenPath));
        Assert.True(File.Exists(result.Artifacts.FailureDevicePath));
        Assert.Contains(result.Screenshots, screenshot => screenshot.Label == "failure-screen");
    }

    [Fact]
    public async Task RunComparesScreenshotAgainstBaseline()
    {
        using var directory = new TemporaryDirectory();
        var baselineDirectory = Path.Combine(directory.Path, "baselines");
        Directory.CreateDirectory(baselineDirectory);
        var baselinePath = Path.Combine(baselineDirectory, "after-ls.png");
        await File.WriteAllBytesAsync(baselinePath, FakeAutomationClient.CapturePngBytes);
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "visual",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") },
            Actions =
            {
                new ScenarioAction { Id = "capture-after-ls", Type = "captureScreen", Label = "after-ls" }
            },
            Assertions =
            {
                new ScenarioAssertion
                {
                    Id = "baseline",
                    Type = "screenshotMatchesBaseline",
                    Label = "after-ls",
                    Baseline = "baselines/after-ls.png",
                    Threshold = 0
                }
            }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath);

        Assert.True(result.Success);
        var assertion = Assert.Single(result.Assertions);
        Assert.True(assertion.Passed);
        Assert.Equal(0, assertion.ChangedRatio);
        Assert.True(File.Exists(Path.Combine(result.Artifacts.RunDirectory, assertion.DiffPath)));
    }

    [Fact]
    public async Task RunCanUpdateMissingBaseline()
    {
        using var directory = new TemporaryDirectory();
        var baselinePath = Path.Combine(directory.Path, "baselines", "after-ls.png");
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "baseline-update",
            DeviceId = "cardputer-zero",
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") },
            Actions =
            {
                new ScenarioAction { Id = "capture-after-ls", Type = "captureScreen", Label = "after-ls" }
            },
            Assertions =
            {
                new ScenarioAssertion
                {
                    Id = "baseline",
                    Type = "screenshotMatchesBaseline",
                    Label = "after-ls",
                    Baseline = "baselines/after-ls.png"
                }
            }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath, new ScenarioRunnerOptions { UpdateBaselines = true });

        Assert.True(result.Success);
        Assert.True(File.Exists(baselinePath));
        Assert.Contains(result.Assertions, assertion => assertion.Message == "Baseline updated.");
    }

    [Fact]
    public async Task RunCanPrepareEnvironmentBeforeStateCapture()
    {
        using var directory = new TemporaryDirectory();
        var scenarioPath = await WriteScenarioAsync(directory.Path, new ScenarioDefinition
        {
            Name = "prepare",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Scale = 1,
            Run = new ScenarioRunOptions { WorkingDir = Path.Combine(directory.Path, "runs") },
            Captures = new ScenarioCaptureOptions { OutputDir = Path.Combine(directory.Path, "runs") }
        });
        var client = new FakeAutomationClient(new AutomationState { Connected = true, DeviceId = "cardputer-zero" });
        var runner = CreateRunner(client);

        var result = await runner.RunAsync(scenarioPath, new ScenarioRunnerOptions { PrepareEnvironment = true });

        Assert.True(result.Success);
        Assert.Contains(client.Methods, method => method == "select_device");
        Assert.Contains(client.Methods, method => method == "set_scale");
        Assert.Contains(client.Methods, method => method == "connect_vnc");
        Assert.True(client.Methods.IndexOf("connect_vnc") < client.Methods.IndexOf("get_state"));
    }

    private static ScenarioRunner CreateRunner(IAutomationClient client) =>
        new(new ScenarioLoader(), new ScenarioValidator(), client, new MarkdownReportWriter());

    private static async Task<string> WriteScenarioAsync(string directory, ScenarioDefinition scenario)
    {
        var path = Path.Combine(directory, "scenario.json");
        await new ScenarioLoader().SaveAsync(scenario, path);
        return path;
    }

    private sealed class FakeAutomationClient : IAutomationClient
    {
        public static readonly byte[] CapturePngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8DwHwQBEPgD/U6VwW8AAAAASUVORK5CYII=");

        private readonly AutomationState state;
        private long frameIndex = 1;

        public FakeAutomationClient(AutomationState state)
        {
            this.state = state;
        }

        public List<string> Methods { get; } = [];

        public async Task<T> SendAsync<T>(string method, object? parameters = null, int timeoutMs = 10000, CancellationToken cancellationToken = default)
        {
            Methods.Add(method);
            object result = method switch
            {
                "get_state" => state,
                "frame_hash" => new FrameHashResult { FrameIndex = frameIndex, FrameHash = $"hash-{frameIndex}", Width = 320, Height = 170 },
                "wait_stable_frame" => new WaitStableFrameResult { Stable = true, FrameIndex = frameIndex, FrameHash = $"hash-{frameIndex}" },
                "select_device" or "set_scale" or "connect_vnc" or "disconnect_vnc" => new AutomationOperationResult { Ok = true, Message = method },
                "capture_screen" => await CaptureAsync(parameters),
                "capture_device" => await CaptureAsync(parameters),
                "type_text" or "press_key" or "press_button" or "click_screen" => Tap(),
                "action_trace" => await TraceAsync(parameters),
                _ => new { ok = true }
            };

            return ConvertResult<T>(result);
        }

        private object Tap()
        {
            frameIndex++;
            return new { ok = true };
        }

        private async Task<CaptureResult> CaptureAsync(object? parameters)
        {
            var path = ReadOutputPath(parameters);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllBytesAsync(path, CapturePngBytes);
            return new CaptureResult { Path = path, Width = 2, Height = 1, FrameIndex = frameIndex };
        }

        private async Task<ActionTraceResult> TraceAsync(object? parameters)
        {
            var path = parameters is ActionTraceParams traceParams ? traceParams.OutputPath : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                await File.WriteAllTextAsync(path, "[]");
            }

            return new ActionTraceResult();
        }

        private static string ReadOutputPath(object? parameters) =>
            parameters is CaptureParams captureParams && !string.IsNullOrWhiteSpace(captureParams.OutputPath)
                ? captureParams.OutputPath
                : Path.GetTempFileName();

        private static T ConvertResult<T>(object result)
        {
            var json = JsonSerializer.Serialize(result, AutomationJson.Options);
            return JsonSerializer.Deserialize<T>(json, AutomationJson.Options)!;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PocketFrameTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
