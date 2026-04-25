namespace PocketFrame.Runner;

public sealed class ScenarioRunnerOptions
{
    public bool GenerateReport { get; set; } = true;
    public int StableQuietMs { get; set; } = 300;
    public int StableTimeoutMs { get; set; } = 5000;
}
