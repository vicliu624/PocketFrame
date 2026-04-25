namespace PocketFrame.Runner;

public sealed class VisualBaselineComparison
{
    public bool Passed { get; set; }
    public string ActualPath { get; set; } = string.Empty;
    public string BaselinePath { get; set; } = string.Empty;
    public string DiffPath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int BaselineWidth { get; set; }
    public int BaselineHeight { get; set; }
    public int ChangedPixels { get; set; }
    public int TotalPixels { get; set; }
    public double ChangedRatio { get; set; }
    public double Threshold { get; set; }
    public string Message { get; set; } = string.Empty;
}
