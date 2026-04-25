namespace PocketFrame.Runner;

public sealed class VisualBaselineCompareOptions
{
    public double Threshold { get; set; }
    public int PixelTolerance { get; set; }
    public List<VisualRegion> Regions { get; set; } = [];
    public List<VisualRegion> IgnoreRegions { get; set; } = [];
    public List<VisualRegion> MaskRegions { get; set; } = [];
}

public sealed record VisualRegion(int X, int Y, int Width, int Height);
