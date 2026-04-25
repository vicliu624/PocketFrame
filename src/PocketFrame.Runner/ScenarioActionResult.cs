namespace PocketFrame.Runner;

public sealed class ScenarioActionResult
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public long FrameIndexBefore { get; set; }
    public long FrameIndexAfter { get; set; }
    public string FrameHashBefore { get; set; } = string.Empty;
    public string FrameHashAfter { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
}
