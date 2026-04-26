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

public sealed class WaitResult
{
    [JsonPropertyName("waitedMs")]
    public int WaitedMs { get; set; }

    [JsonPropertyName("frameIndex")]
    public long FrameIndex { get; set; }

    [JsonPropertyName("frameHash")]
    public string FrameHash { get; set; } = string.Empty;
}

public sealed class AutomationActionTraceEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

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
    public List<ReplayLogEntryResult> Entries { get; set; } = [];
}

public sealed class ReplayLogEntryResult
{
    [JsonPropertyName("originalEntry")]
    public AutomationActionTraceEntry OriginalEntry { get; set; } = new();

    [JsonPropertyName("replayEntry")]
    public AutomationActionTraceEntry ReplayEntry { get; set; } = new();
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

public sealed class InputActionResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; } = true;

    [JsonPropertyName("requestedAction")]
    public string RequestedAction { get; set; } = string.Empty;

    [JsonPropertyName("requestedKey")]
    public string RequestedKey { get; set; } = string.Empty;

    [JsonPropertyName("requestedButtonId")]
    public string RequestedButtonId { get; set; } = string.Empty;

    [JsonPropertyName("inputLayer")]
    public string InputLayer { get; set; } = string.Empty;

    [JsonPropertyName("activeLayersBefore")]
    public List<string> ActiveLayersBefore { get; set; } = [];

    [JsonPropertyName("activeLayersAfter")]
    public List<string> ActiveLayersAfter { get; set; } = [];

    [JsonPropertyName("resolved")]
    public InputResolvedButton Resolved { get; set; } = new();

    [JsonPropertyName("emitted")]
    public List<InputEmittedEvent> Emitted { get; set; } = [];

    [JsonPropertyName("resolvedKey")]
    public string ResolvedKey { get; set; } = string.Empty;

    [JsonPropertyName("emittedTransport")]
    public string EmittedTransport { get; set; } = string.Empty;

    [JsonPropertyName("emittedKey")]
    public string EmittedKey { get; set; } = string.Empty;

    [JsonPropertyName("emittedKeysym")]
    public string EmittedKeysym { get; set; } = string.Empty;

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class InputResolvedButton
{
    [JsonPropertyName("profileButtonId")]
    public string ProfileButtonId { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("selectedLayer")]
    public string SelectedLayer { get; set; } = string.Empty;

    [JsonPropertyName("resolvedKey")]
    public string ResolvedKey { get; set; } = string.Empty;

    [JsonPropertyName("sourceKey")]
    public string SourceKey { get; set; } = string.Empty;
}

public sealed class InputEmittedEvent
{
    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "vnc";

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("keysym")]
    public string Keysym { get; set; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "tap";
}

public sealed class InputTruthWarning
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class ButtonPressParams
{
    [JsonPropertyName("buttonId")]
    public string ButtonId { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }
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

public sealed class WaitParams
{
    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; } = 1000;
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

public sealed class DeviceProfilesResult
{
    [JsonPropertyName("profiles")]
    public List<DeviceProfileSummary> Profiles { get; set; } = [];
}

public sealed class DeviceProfileSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("screenWidth")]
    public int ScreenWidth { get; set; }

    [JsonPropertyName("screenHeight")]
    public int ScreenHeight { get; set; }

    [JsonPropertyName("shellWidth")]
    public double ShellWidth { get; set; }

    [JsonPropertyName("shellHeight")]
    public double ShellHeight { get; set; }
}

public sealed class ConnectionProfilesResult
{
    [JsonPropertyName("connections")]
    public List<ConnectionProfileSummary> Connections { get; set; } = [];
}

public sealed class ConnectionProfileSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("scale")]
    public double Scale { get; set; }

    [JsonPropertyName("hasPassword")]
    public bool HasPassword { get; set; }
}

public sealed class SelectDeviceParams
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class SetScaleParams
{
    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1;
}

public sealed class ConnectVncParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1;
}

public sealed class AutomationOperationResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; } = true;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class InputModelResult
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = [];

    [JsonPropertyName("buttons")]
    public List<InputButtonSummary> Buttons { get; set; } = [];

    [JsonPropertyName("keys")]
    public List<InputKeySummary> Keys { get; set; } = [];
}

public sealed class InputButtonSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("shortPressKey")]
    public string ShortPressKey { get; set; } = string.Empty;

    [JsonPropertyName("longPressKey")]
    public string LongPressKey { get; set; } = string.Empty;

    [JsonPropertyName("supportsLongPress")]
    public bool SupportsLongPress { get; set; }
}

public sealed class InputKeySummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonPropertyName("normal")]
    public string Normal { get; set; } = string.Empty;

    [JsonPropertyName("fn")]
    public string Fn { get; set; } = string.Empty;

    [JsonPropertyName("sym")]
    public string Sym { get; set; } = string.Empty;

    [JsonPropertyName("shift")]
    public string Shift { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public sealed class KeyboardStateResult
{
    [JsonPropertyName("activeLayers")]
    public List<string> ActiveLayers { get; set; } = [];
}
