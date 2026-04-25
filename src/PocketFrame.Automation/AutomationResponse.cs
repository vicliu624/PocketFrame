using System.Text.Json.Serialization;

namespace PocketFrame.Automation;

public sealed class AutomationResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public AutomationError? Error { get; set; }

    public static AutomationResponse Success(string id, object? result) => new()
    {
        Id = id,
        Ok = true,
        Result = result
    };

    public static AutomationResponse Failure(string id, string code, string message) => new()
    {
        Id = id,
        Ok = false,
        Error = new AutomationError { Code = code, Message = message }
    };
}

public sealed class AutomationError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
