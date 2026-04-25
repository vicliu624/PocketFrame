using System.Text.Json.Serialization;

namespace PocketFrame.Scenarios;

public sealed class ScenarioDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("connection")]
    public ScenarioConnection Connection { get; set; } = new();

    [JsonPropertyName("environment")]
    public ScenarioEnvironment Environment { get; set; } = new();

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1;

    [JsonPropertyName("captures")]
    public ScenarioCaptureOptions Captures { get; set; } = new();

    [JsonPropertyName("run")]
    public ScenarioRunOptions Run { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<ScenarioAction> Actions { get; set; } = [];

    [JsonPropertyName("assertions")]
    public List<ScenarioAssertion> Assertions { get; set; } = [];
}

public sealed class ScenarioConnection
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 5900;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ScenarioEnvironment
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("distro")]
    public string Distro { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("prepare")]
    public bool Prepare { get; set; }

    [JsonPropertyName("startVnc")]
    public bool StartVnc { get; set; } = true;

    [JsonPropertyName("restartVnc")]
    public bool RestartVnc { get; set; }

    [JsonPropertyName("vncDisplay")]
    public string VncDisplay { get; set; } = string.Empty;

    [JsonPropertyName("vncGeometry")]
    public string VncGeometry { get; set; } = string.Empty;

    [JsonPropertyName("vncDepth")]
    public int VncDepth { get; set; }

    [JsonPropertyName("preCommands")]
    public List<ScenarioEnvironmentCommand> PreCommands { get; set; } = [];

    [JsonPropertyName("postCommands")]
    public List<ScenarioEnvironmentCommand> PostCommands { get; set; } = [];
}

public sealed class ScenarioEnvironmentCommand
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }

    [JsonPropertyName("continueOnFailure")]
    public bool ContinueOnFailure { get; set; }
}

public sealed class ScenarioCaptureOptions
{
    [JsonPropertyName("outputDir")]
    public string OutputDir { get; set; } = "runs/default";
}

public sealed class ScenarioRunOptions
{
    [JsonPropertyName("workingDir")]
    public string WorkingDir { get; set; } = "runs/default";

    [JsonPropertyName("captureOnFailure")]
    public bool CaptureOnFailure { get; set; }
}

public sealed class ScenarioAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("quietMs")]
    public int QuietMs { get; set; } = 300;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 5000;

    [JsonPropertyName("afterFrame")]
    public long? AfterFrame { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("buttonId")]
    public string ButtonId { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("button")]
    public string Button { get; set; } = "left";

    [JsonPropertyName("captureOnFailure")]
    public bool? CaptureOnFailure { get; set; }
}

public sealed class ScenarioAssertion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("afterAction")]
    public string AfterAction { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("baseline")]
    public string Baseline { get; set; } = string.Empty;

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("pixelTolerance")]
    public int PixelTolerance { get; set; }

    [JsonPropertyName("expectedHash")]
    public string ExpectedHash { get; set; } = string.Empty;

    [JsonPropertyName("regions")]
    public List<ScenarioRegion> Regions { get; set; } = [];

    [JsonPropertyName("ignoreRegions")]
    public List<ScenarioRegion> IgnoreRegions { get; set; } = [];

    [JsonPropertyName("maskRegions")]
    public List<ScenarioRegion> MaskRegions { get; set; } = [];
}

public sealed class ScenarioRegion
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
