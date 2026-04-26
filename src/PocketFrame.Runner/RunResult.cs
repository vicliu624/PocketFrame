namespace PocketFrame.Runner;

public sealed class RunResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public RunArtifacts Artifacts { get; set; } = new();
    public List<string> Errors { get; set; } = [];
    public List<PocketFrame.Environments.EnvironmentCommandResult> EnvironmentCommands { get; set; } = [];
    public PocketFrame.Environments.EnvironmentAppStatusResult? AppStatus { get; set; }
    public List<ScenarioActionResult> Actions { get; set; } = [];
    public List<ScenarioAssertionResult> Assertions { get; set; } = [];
    public List<PocketFrame.Reports.ReportScreenshot> Screenshots { get; set; } = [];
}
