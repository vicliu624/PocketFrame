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
}
