namespace PocketFrame.Runner;

public sealed class RunResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public RunArtifacts Artifacts { get; set; } = new();
    public List<string> Errors { get; set; } = [];
}
