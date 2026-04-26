using PocketFrame.Scenarios;

namespace PocketFrame.Scenarios.Tests;

public sealed class ScenarioValidatorTests
{
    [Fact]
    public void ValidateAcceptsMinimalScenario()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "smoke",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Scale = 1,
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/smoke" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/smoke" }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidPort()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "smoke",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 70000 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/smoke" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/smoke" }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAcceptsVisualBaselineAssertion()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "visual",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/visual" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/visual", CaptureOnFailure = true },
            Assertions =
            {
                new ScenarioAssertion
                {
                    Id = "matches",
                    Type = "screenshotMatchesBaseline",
                    Label = "after-ls",
                    Baseline = "baselines/after-ls.png",
                    Threshold = 0.02,
                    PixelTolerance = 8,
                    Regions = { new ScenarioRegion { X = 0, Y = 0, Width = 320, Height = 150 } },
                    IgnoreRegions = { new ScenarioRegion { X = 300, Y = 0, Width = 40, Height = 20 } },
                    MaskRegions = { new ScenarioRegion { X = 0, Y = 160, Width = 320, Height = 10 } }
                },
                new ScenarioAssertion { Id = "hash-equals", Type = "frameHashEquals", ExpectedHash = "abc" },
                new ScenarioAssertion { Id = "actions-ok", Type = "allActionsSucceeded" }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsPressButtonHoldDuration()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "hold-button",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/hold-button" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/hold-button" },
            Actions =
            {
                new ScenarioAction { Id = "hold-next", Type = "pressButton", ButtonId = "next-home", DurationMs = 800 }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsWaitActions()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "waits",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/waits" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/waits" },
            Actions =
            {
                new ScenarioAction { Id = "plain-wait", Type = "wait", DurationMs = 1000 },
                new ScenarioAction { Id = "frame-change", Type = "waitFrameChange", TimeoutMs = 3000 }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidWaitActions()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "bad-waits",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/bad-waits" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/bad-waits" },
            Actions =
            {
                new ScenarioAction { Id = "plain-wait", Type = "wait", DurationMs = 0 },
                new ScenarioAction { Id = "frame-change", Type = "waitFrameChange", TimeoutMs = 0, AfterFrame = -1 }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("durationMs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("timeoutMs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("afterFrame", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRejectsNegativePressButtonHoldDuration()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "bad-hold",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/bad-hold" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/bad-hold" },
            Actions =
            {
                new ScenarioAction { Id = "hold-next", Type = "pressButton", ButtonId = "next-home", DurationMs = -1 }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("durationMs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAcceptsAppLifecycleAndSemanticAssertions()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "app-lifecycle",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/app-lifecycle" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/app-lifecycle" },
            Environment = new ScenarioEnvironment { ProfileId = "wsl-ubuntu" },
            App = new ScenarioApp
            {
                Id = "lofibox",
                Command = "./lofibox",
                WorkingDirectory = "/home/user/lofibox",
                ProcessMatch = "lofibox",
                LogPath = ".tmp/lofibox.log",
                ClearPaths = { ".tmp/state", ".tmp/cache" },
                Env = { ["XDG_STATE_HOME"] = ".tmp/state" },
                KillBeforeLaunch = true,
                CleanupOnFinish = true
            },
            Assertions =
            {
                new ScenarioAssertion { Id = "log-main", Type = "logContains", Text = "page=MainMenu" },
                new ScenarioAssertion { Id = "json-page", Type = "jsonEquals", Path = "lofibox-state.json", Selector = "page", Expected = "MainMenu" }
            }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsAppLifecycleWithoutEnvironmentProfile()
    {
        var scenario = new ScenarioDefinition
        {
            Name = "bad-app",
            DeviceId = "cardputer-zero",
            Connection = new ScenarioConnection { Host = "127.0.0.1", Port = 5910 },
            Captures = new ScenarioCaptureOptions { OutputDir = "runs/bad-app" },
            Run = new ScenarioRunOptions { WorkingDir = "runs/bad-app" },
            App = new ScenarioApp { Id = "lofibox", KillBeforeLaunch = true }
        };

        var result = new ScenarioValidator().Validate(scenario);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("environment.profileId", StringComparison.OrdinalIgnoreCase));
    }
}
