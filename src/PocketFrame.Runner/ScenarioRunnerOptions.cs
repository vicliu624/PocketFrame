namespace PocketFrame.Runner;

public sealed class ScenarioRunnerOptions
{
    public bool GenerateReport { get; set; } = true;
    public bool UpdateBaselines { get; set; }
    public int StableQuietMs { get; set; } = 300;
    public int StableTimeoutMs { get; set; } = 5000;
}
