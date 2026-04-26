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
                new AutomationActionTraceEntry { Method = "type_text", Ok = true, FrameIndexBefore = 10, FrameIndexAfter = 11 },
                new AutomationActionTraceEntry
                {
                    Method = "press_button",
                    Ok = true,
                    Result = new InputActionResult
                    {
                        RequestedAction = "press_button",
                        RequestedButtonId = "keyboard-z",
                        InputLayer = "pocketframe-profile",
                        ResolvedKey = "Left",
                        EmittedKey = "Left",
                        EmittedKeysym = "0xff51",
                        Warnings = { "VNC key events are not Linux evdev events." }
                    }
                }
            },
            Screenshots =
            {
                new ReportScreenshot { Label = "Initial screen", Path = "screenshots/initial-screen.png", FrameIndex = 11, FrameHash = "def" }
            },
            AssertionResults =
            {
                new ReportAssertionResult
                {
                    Id = "visual",
                    Type = "screenshotMatchesBaseline",
                    Passed = false,
                    BaselinePath = "baseline.png",
                    ActualPath = "actual.png",
                    DiffPath = "diff.png",
                    ChangedPixels = 2,
                    TotalPixels = 10,
                    ChangedRatio = 0.2,
                    Threshold = 0.1,
                    PixelTolerance = 8
                }
            }
        };

        var markdown = new MarkdownReportWriter().Build(report);

        Assert.Contains("smoke", markdown);
        Assert.Contains("type_text", markdown);
        Assert.Contains("initial-screen.png", markdown);
        Assert.Contains("## Visual Diffs", markdown);
        Assert.Contains("diff.png", markdown);
        Assert.Contains("## Input Audit", markdown);
        Assert.Contains("keyboard-z", markdown);
        Assert.Contains("0xff51", markdown);
    }
}
