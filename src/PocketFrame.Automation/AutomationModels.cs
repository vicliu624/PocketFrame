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

public sealed class WaitFrameResult
{
    [JsonPropertyName("changed")]
    public bool Changed { get; set; }

    [JsonPropertyName("previousFrameIndex")]
    public long PreviousFrameIndex { get; set; }

    [JsonPropertyName("currentFrameIndex")]
    public long CurrentFrameIndex { get; set; }
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
