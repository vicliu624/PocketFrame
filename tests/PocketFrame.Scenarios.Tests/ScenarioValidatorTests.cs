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
}
