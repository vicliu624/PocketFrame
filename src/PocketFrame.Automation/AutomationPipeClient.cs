using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PocketFrame.Automation;

public sealed class AutomationPipeClient
{
    private readonly string pipeName;

    public AutomationPipeClient(string pipeName = AutomationPipeNames.DefaultPipeName)
    {
        this.pipeName = pipeName;
    }

    public async Task<AutomationResponse> SendAsync(string method, object? parameters = null, int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("PocketFrame.App is not running or the automation pipe is not ready.");
        }

        var command = new AutomationCommand
        {
            Method = method,
            Params = parameters is null ? null : JsonSerializer.SerializeToElement(parameters, AutomationJson.Options)
        };

        await WriteLineAsync(pipe, JsonSerializer.Serialize(command, AutomationJson.Options), timeout.Token);
        var line = await ReadLineAsync(pipe, timeout.Token);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new IOException("PocketFrame automation pipe closed without a response.");
        }

        return JsonSerializer.Deserialize<AutomationResponse>(line, AutomationJson.Options) ??
               throw new JsonException("PocketFrame automation pipe returned an invalid response.");
    }

    private static async Task WriteLineAsync(Stream stream, string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0)
            {
                return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (one[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');
            }

            buffer.Add(one[0]);
        }
    }
}
