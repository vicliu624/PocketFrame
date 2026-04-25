using System.Text.Json;
using PocketFrame.Automation;

namespace PocketFrame.Automation.Tests;

public sealed class AutomationSerializationTests
{
    [Fact]
    public void ActionTraceEntryRoundTripsThroughJson()
    {
        var entry = new AutomationActionTraceEntry
        {
            Id = "1",
            Method = "press_key",
            FrameHashBefore = "before",
            FrameHashAfter = "after",
            FrameIndexBefore = 1,
            FrameIndexAfter = 2,
            Ok = true
        };

        var json = JsonSerializer.Serialize(entry, AutomationJson.Options);
        var roundTrip = JsonSerializer.Deserialize<AutomationActionTraceEntry>(json, AutomationJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal("press_key", roundTrip.Method);
        Assert.Equal("before", roundTrip.FrameHashBefore);
        Assert.Equal("after", roundTrip.FrameHashAfter);
    }

    [Fact]
    public void ReplayResultSeparatesOriginalAndReplayEntries()
    {
        var result = new ReplayLogResult
        {
            Replayed = 1,
            Entries =
            {
                new ReplayLogEntryResult
                {
                    OriginalEntry = new AutomationActionTraceEntry { Method = "press_key", FrameHashBefore = "old" },
                    ReplayEntry = new AutomationActionTraceEntry { Method = "press_key", FrameHashBefore = "new" }
                }
            }
        };

        var json = JsonSerializer.Serialize(result, AutomationJson.Options);
        var roundTrip = JsonSerializer.Deserialize<ReplayLogResult>(json, AutomationJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal("old", roundTrip.Entries[0].OriginalEntry.FrameHashBefore);
        Assert.Equal("new", roundTrip.Entries[0].ReplayEntry.FrameHashBefore);
    }

    [Fact]
    public void ButtonPressParamsPreserveHoldDuration()
    {
        var json = """
            {
              "buttonId": "next-home",
              "durationMs": 800
            }
            """;

        var parameters = JsonSerializer.Deserialize<ButtonPressParams>(json, AutomationJson.Options);

        Assert.NotNull(parameters);
        Assert.Equal("next-home", parameters.ButtonId);
        Assert.Equal(800, parameters.DurationMs);
    }

    [Fact]
    public void WaitParamsRoundTripsThroughJson()
    {
        var parameters = new WaitParams { DurationMs = 1500 };

        var json = JsonSerializer.Serialize(parameters, AutomationJson.Options);
        var roundTrip = JsonSerializer.Deserialize<WaitParams>(json, AutomationJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal(1500, roundTrip.DurationMs);
    }

    [Fact]
    public void InputButtonSummaryExposesLongPressMetadata()
    {
        var summary = new InputButtonSummary
        {
            Id = "next-home",
            Label = "NEXT",
            Key = "Right",
            ShortPressKey = "Right",
            LongPressKey = "Home",
            SupportsLongPress = true
        };

        var json = JsonSerializer.Serialize(summary, AutomationJson.Options);

        Assert.Contains("\"shortPressKey\":\"Right\"", json);
        Assert.Contains("\"longPressKey\":\"Home\"", json);
        Assert.Contains("\"supportsLongPress\":true", json);
    }
}
