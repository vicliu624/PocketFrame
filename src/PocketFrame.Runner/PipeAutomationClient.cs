using System.Text.Json;
using PocketFrame.Automation;

namespace PocketFrame.Runner;

public sealed class PipeAutomationClient : IAutomationClient
{
    private readonly AutomationPipeClient client;

    public PipeAutomationClient() : this(new AutomationPipeClient())
    {
    }

    public PipeAutomationClient(AutomationPipeClient client)
    {
        this.client = client;
    }

    public async Task<T> SendAsync<T>(string method, object? parameters = null, int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        var response = await client.SendAsync(method, parameters, timeoutMs, cancellationToken);
        if (!response.Ok)
        {
            throw new InvalidOperationException($"{response.Error?.Code}: {response.Error?.Message}");
        }

        return ConvertResult<T>(response.Result);
    }

    private static T ConvertResult<T>(object? result)
    {
        if (result is JsonElement element)
        {
            return element.Deserialize<T>(AutomationJson.Options) ??
                   throw new InvalidOperationException($"Unable to deserialize automation result as {typeof(T).Name}.");
        }

        var json = JsonSerializer.Serialize(result, AutomationJson.Options);
        return JsonSerializer.Deserialize<T>(json, AutomationJson.Options) ??
               throw new InvalidOperationException($"Unable to deserialize automation result as {typeof(T).Name}.");
    }
}
