using PocketFrame.Automation;
using PocketFrame.Reports;
using PocketFrame.Scenarios;

namespace PocketFrame.Reports.Tests;

public sealed class MarkdownReportWriterTests
{
    [Fact]
    public void BuildIncludesScenarioTraceAndScreenshots()
    {
        var report = new AutomationRunReport
        {
            RunId = "run",
            RunDirectory = "runs/run",
            Scenario = new ScenarioDefinition { Name = "smoke", DeviceId = "cardputer-zero" },
            State = new AutomationState { Connected = true, FrameIndex = 10, FrameHash = "abc", VncStatus = "Connected" },
            Trace =
            {
                new AutomationActionTraceEntry { Method = "type_text", Ok = true, FrameIndexBefore = 10, FrameIndexAfter = 11 }
            },
            Screenshots =
            {
                new ReportScreenshot { Label = "Initial screen", Path = "screenshots/initial-screen.png", FrameIndex = 11, FrameHash = "def" }
            }
        };

        var markdown = new MarkdownReportWriter().Build(report);

        Assert.Contains("smoke", markdown);
        Assert.Contains("type_text", markdown);
        Assert.Contains("initial-screen.png", markdown);
    }
}
