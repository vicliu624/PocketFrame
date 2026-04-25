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

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1;

    [JsonPropertyName("captures")]
    public ScenarioCaptureOptions Captures { get; set; } = new();

    [JsonPropertyName("run")]
    public ScenarioRunOptions Run { get; set; } = new();
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

public sealed class ScenarioCaptureOptions
{
    [JsonPropertyName("outputDir")]
    public string OutputDir { get; set; } = "runs/default";
}

public sealed class ScenarioRunOptions
{
    [JsonPropertyName("workingDir")]
    public string WorkingDir { get; set; } = "runs/default";
}
