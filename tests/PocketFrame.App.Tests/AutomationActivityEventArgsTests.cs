using System.Text.Json;
using PocketFrame.App.Automation;

namespace PocketFrame.App.Tests;

public sealed class AutomationActivityEventArgsTests
{
    [Fact]
    public void StartedDisplayTextHandlesNullNumericFields()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "profileId": "wsl-ubuntu-24.04",
              "pid": null,
              "match": "lofibox",
              "timeoutMs": null
            }
            """);

        var activity = AutomationActivityEventArgs.Started(
            "environment_kill_process",
            "test-command",
            document.RootElement.Clone(),
            DateTimeOffset.Parse("2026-04-26T15:31:51+08:00"));

        Assert.Contains("kill env process lofibox", activity.DisplayText);
    }
}
