using PocketFrame.Automation;
using PocketFrame.Scenarios;

namespace PocketFrame.Reports;

public sealed class AutomationRunReport
{
    public string RunId { get; set; } = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
    public string RunDirectory { get; set; } = string.Empty;
    public ScenarioDefinition? Scenario { get; set; }
    public AutomationState? State { get; set; }
    public bool Success { get; set; }
    public List<AutomationActionTraceEntry> Trace { get; set; } = [];
    public List<ReportActionResult> ActionResults { get; set; } = [];
    public List<ReportAssertionResult> AssertionResults { get; set; } = [];
    public List<ReportScreenshot> Screenshots { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class ReportActionResult
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

public sealed class ReportAssertionResult
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ReportScreenshot
{
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long FrameIndex { get; set; }
    public string FrameHash { get; set; } = string.Empty;
}
