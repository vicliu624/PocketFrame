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
}
