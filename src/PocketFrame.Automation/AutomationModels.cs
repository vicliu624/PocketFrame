using System.Text.Json.Serialization;

namespace PocketFrame.Automation;

public sealed class AutomationState
{
    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("screenWidth")]
    public int ScreenWidth { get; set; }

    [JsonPropertyName("screenHeight")]
    public int ScreenHeight { get; set; }

    [JsonPropertyName("shellWidth")]
    public double ShellWidth { get; set; }

    [JsonPropertyName("shellHeight")]
    public double ShellHeight { get; set; }

    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }

    [JsonPropertyName("frameHash")]
    public string FrameHash { get; set; } = string.Empty;

    [JsonPropertyName("vncStatus")]
    public string VncStatus { get; set; } = string.Empty;

    [JsonPropertyName("lastFrameUpdatedAt")]
    public DateTimeOffset? LastFrameUpdatedAt { get; set; }
}

public sealed class CaptureResult
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }
}

public sealed class FrameHashResult
{
    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }

    [JsonPropertyName("frameHash")]
    public string FrameHash { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public sealed class WaitFrameResult
{
    [JsonPropertyName("changed")]
    public bool Changed { get; set; }

    [JsonPropertyName("previousFrameIndex")]
    public long PreviousFrameIndex { get; set; }

    [JsonPropertyName("currentFrameIndex")]
    public long CurrentFrameIndex { get; set; }
}

public sealed class WaitStableFrameResult
{
    [JsonPropertyName("stable")]
    public bool Stable { get; set; }

    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }

    [JsonPropertyName("frameHash")]
    public string FrameHash { get; set; } = string.Empty;

    [JsonPropertyName("quietMs")]
    public int QuietMs { get; set; }

    [JsonPropertyName("elapsedMs")]
    public int ElapsedMs { get; set; }
}

public sealed class AutomationActionTraceEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("finishedAt")]
    public DateTimeOffset? FinishedAt { get; set; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("frameIndexBefore")]
    public long FrameIndexBefore { get; set; }

    [JsonPropertyName("frameIndexAfter")]
    public long FrameIndexAfter { get; set; }

    [JsonPropertyName("frameHashBefore")]
    public string FrameHashBefore { get; set; } = string.Empty;

    [JsonPropertyName("frameHashAfter")]
    public string FrameHashAfter { get; set; } = string.Empty;
}

public sealed class ActionTraceResult
{
    [JsonPropertyName("entries")]
    public List<AutomationActionTraceEntry> Entries { get; set; } = [];

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

public sealed class ReplayLogResult
{
    [JsonPropertyName("replayed")]
    public int Replayed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("entries")]
    public List<AutomationActionTraceEntry> Entries { get; set; } = [];
}

public sealed class TextInputParams
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class KeyPressParams
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

public sealed class ButtonPressParams
{
    [JsonPropertyName("buttonId")]
    public string ButtonId { get; set; } = string.Empty;
}

public sealed class ClickScreenParams
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("button")]
    public string Button { get; set; } = "left";
}

public sealed class CaptureParams
{
    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }
}

public sealed class WaitFrameChangeParams
{
    [JsonPropertyName("afterFrame")]
    public long? AfterFrame { get; set; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 3000;
}

public sealed class WaitStableFrameParams
{
    [JsonPropertyName("quietMs")]
    public int QuietMs { get; set; } = 300;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 5000;
}

public sealed class ActionTraceParams
{
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("clear")]
    public bool Clear { get; set; }
}

public sealed class ReplayLogParams
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("delayMs")]
    public int DelayMs { get; set; }
}
