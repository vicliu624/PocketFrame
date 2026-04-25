using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketFrame.Automation;

public sealed class AutomationCommand
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}
