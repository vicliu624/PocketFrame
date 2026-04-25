using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PocketFrame.App.Utils;
using PocketFrame.Automation;

namespace PocketFrame.App.Automation;

public sealed class AutomationPipeServer : IAsyncDisposable
{
    private readonly IAutomationService automationService;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? serverTask;

    public AutomationPipeServer(IAutomationService automationService)
    {
        this.automationService = automationService;
    }

    public bool IsRunning => serverTask is not null && cancellationTokenSource is not null && !cancellationTokenSource.IsCancellationRequested;
    public string LastCommand { get; private set; } = "None";
    public string LastResult { get; private set; } = "None";
    public string LastError { get; private set; } = string.Empty;
    public DateTimeOffset? LastCommandAt { get; private set; }
    public DateTimeOffset? LastResponseAt { get; private set; }

    public void Start()
    {
        if (serverTask is not null)
        {
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        serverTask = Task.Run(() => RunAsync(cancellationTokenSource.Token));
        InputDiagnostics.Write("Automation", $"Pipe server started: {AutomationPipeNames.DefaultPipeName}");
    }

    public async ValueTask DisposeAsync()
    {
        cancellationTokenSource?.Cancel();
        if (serverTask is not null)
        {
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellationTokenSource?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                AutomationPipeNames.DefaultPipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(cancellationToken);
            await HandleClientAsync(pipe, cancellationToken);
        }
    }

    private async Task HandleClientAsync(Stream pipe, CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(pipe, cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var command = JsonSerializer.Deserialize<AutomationCommand>(line, AutomationJson.Options);
        if (command is null)
        {
            await WriteLineAsync(pipe, JsonSerializer.Serialize(AutomationResponse.Failure(string.Empty, "invalid_json", "Invalid automation command."), AutomationJson.Options), cancellationToken);
            return;
        }

        InputDiagnostics.Write("Automation", $"Command {command.Method} ({command.Id})");
        LastCommand = $"{command.Method} ({command.Id})";
        LastCommandAt = DateTimeOffset.Now;
        var response = await DispatchAsync(command);
        LastResponseAt = DateTimeOffset.Now;
        LastResult = response.Ok ? "OK" : "Error";
        LastError = response.Error?.Message ?? string.Empty;
        await WriteLineAsync(pipe, JsonSerializer.Serialize(response, AutomationJson.Options), cancellationToken);
    }

    private async Task<AutomationResponse> DispatchAsync(AutomationCommand command)
    {
        try
        {
            object? result = command.Method switch
            {
                "get_state" => await automationService.GetStateAsync(),
                "capture_screen" => await automationService.CaptureScreenAsync(ReadParams<CaptureParams>(command).OutputPath),
                "capture_device" => await automationService.CaptureDeviceAsync(ReadParams<CaptureParams>(command).OutputPath),
                "type_text" => await RunAsync(async () => await automationService.TypeTextAsync(ReadParams<TextInputParams>(command).Text)),
                "press_key" => await RunAsync(async () => await automationService.PressKeyAsync(ReadParams<KeyPressParams>(command).Key)),
                "press_button" => await RunAsync(async () => await automationService.PressButtonAsync(ReadParams<ButtonPressParams>(command).ButtonId)),
                "click_screen" => await RunAsync(async () =>
                {
                    var parameters = ReadParams<ClickScreenParams>(command);
                    await automationService.ClickScreenAsync(parameters.X, parameters.Y, parameters.Button);
                }),
                "wait_frame_change" => await automationService.WaitFrameChangeAsync(ReadParams<WaitFrameChangeParams>(command).AfterFrame, ReadParams<WaitFrameChangeParams>(command).TimeoutMs),
                _ => throw new NotSupportedException($"Unsupported automation method '{command.Method}'.")
            };

            return AutomationResponse.Success(command.Id, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            InputDiagnostics.Write("Automation", $"Command failed {command.Method}: {ex.Message}");
            return AutomationResponse.Failure(command.Id, ex.GetType().Name, ex.Message);
        }
    }

    private static T ReadParams<T>(AutomationCommand command)
    {
        if (command.Params is null)
        {
            return Activator.CreateInstance<T>();
        }

        return command.Params.Value.Deserialize<T>(AutomationJson.Options) ??
               Activator.CreateInstance<T>();
    }

    private static async Task<object> RunAsync(Func<Task> action)
    {
        await action();
        return new { ok = true };
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
