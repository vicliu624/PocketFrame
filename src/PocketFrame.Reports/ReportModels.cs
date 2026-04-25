using PocketFrame.Automation;
using PocketFrame.Scenarios;

namespace PocketFrame.Reports;

public sealed class AutomationRunReport
{
    public string RunId { get; set; } = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
    public string RunDirectory { get; set; } = string.Empty;
    public ScenarioDefinition? Scenario { get; set; }
    public AutomationState? State { get; set; }
    public List<AutomationActionTraceEntry> Trace { get; set; } = [];
    public List<ReportScreenshot> Screenshots { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class ReportScreenshot
{
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long FrameIndex { get; set; }
    public string FrameHash { get; set; } = string.Empty;
}
