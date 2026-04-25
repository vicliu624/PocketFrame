namespace PocketFrame.Runner;

public sealed class ScenarioAssertionResult
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string BaselinePath { get; set; } = string.Empty;
    public string ActualPath { get; set; } = string.Empty;
    public string DiffPath { get; set; } = string.Empty;
    public int ChangedPixels { get; set; }
    public int TotalPixels { get; set; }
    public double ChangedRatio { get; set; }
    public double Threshold { get; set; }
}
