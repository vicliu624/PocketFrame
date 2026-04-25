using PocketFrame.Scenarios;

namespace PocketFrame.Runner;

public sealed class RunContext
{
    public ScenarioDefinition Scenario { get; set; } = new();
    public string SourceScenarioPath { get; set; } = string.Empty;
    public string RunDirectory { get; set; } = string.Empty;
    public string ScreenshotsDirectory => Path.Combine(RunDirectory, "screenshots");
}
