namespace PocketFrame.Runner;

public sealed class RunArtifacts
{
    public string RunDirectory { get; set; } = string.Empty;
    public string SourceScenarioPath { get; set; } = string.Empty;
    public string ScenarioPath { get; set; } = string.Empty;
    public string StatePath { get; set; } = string.Empty;
    public string EnvironmentStatePath { get; set; } = string.Empty;
    public string TracePath { get; set; } = string.Empty;
    public string InitialScreenPath { get; set; } = string.Empty;
    public string InitialDevicePath { get; set; } = string.Empty;
    public string FailureScreenPath { get; set; } = string.Empty;
    public string FailureDevicePath { get; set; } = string.Empty;
    public string ReportPath { get; set; } = string.Empty;
}
