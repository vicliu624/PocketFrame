using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PocketFrame.App.Utils;
using PocketFrame.Automation;

namespace PocketFrame.App.Automation;

public sealed class AutomationPipeServer : IAsyncDisposable
{
    private readonly IAutomationService automationService;
    private readonly object traceSync = new();
    private readonly List<AutomationActionTraceEntry> actionTrace = [];
    private CancellationTokenSource? cancellationTokenSource;
    private Task? serverTask;
    private const int MaxTraceEntries = 1000;

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
            await WriteLineAsync(pipe, JsonSerializer.Serialize(AutomationResponse.Failure(string.Empty, AutomationErrorCodes.InvalidJson, "Invalid automation command."), AutomationJson.Options), cancellationToken);
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
        var traceEntry = IsTraceable(command.Method) ? await BeginTraceEntryAsync(command) : null;
        try
        {
            object? result = command.Method switch
            {
                "get_state" => await automationService.GetStateAsync(),
                "frame_hash" => await automationService.GetFrameHashAsync(),
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
                "wait_stable_frame" => await automationService.WaitStableFrameAsync(ReadParams<WaitStableFrameParams>(command).QuietMs, ReadParams<WaitStableFrameParams>(command).TimeoutMs),
                "action_trace" => ActionTrace(ReadParams<ActionTraceParams>(command)),
                "replay_log" => await ReplayLogAsync(ReadParams<ReplayLogParams>(command)),
                _ => throw new AutomationException(AutomationErrorCodes.UnsupportedMethod, $"Unsupported automation method '{command.Method}'.")
            };

            var response = AutomationResponse.Success(command.Id, result ?? new { ok = true });
            await CompleteTraceEntryAsync(traceEntry, response);
            return response;
        }
        catch (AutomationException ex)
        {
            InputDiagnostics.Write("Automation", $"Command failed {command.Method}: {ex.Message}");
            var response = AutomationResponse.Failure(command.Id, ex);
            await CompleteTraceEntryAsync(traceEntry, response);
            return response;
        }
        catch (OperationCanceledException ex)
        {
            InputDiagnostics.Write("Automation", $"Command cancelled {command.Method}: {ex.Message}");
            var response = AutomationResponse.Failure(command.Id, AutomationErrorCodes.Cancelled, ex.Message);
            await CompleteTraceEntryAsync(traceEntry, response);
            return response;
        }
        catch (IOException ex)
        {
            InputDiagnostics.Write("Automation", $"Command IO failed {command.Method}: {ex.Message}");
            var response = AutomationResponse.Failure(command.Id, AutomationErrorCodes.IoError, ex.Message);
            await CompleteTraceEntryAsync(traceEntry, response);
            return response;
        }
        catch (Exception ex)
        {
            InputDiagnostics.Write("Automation", $"Command failed {command.Method}: {ex.Message}");
            var response = AutomationResponse.Failure(command.Id, AutomationErrorCodes.InternalError, ex.Message);
            await CompleteTraceEntryAsync(traceEntry, response);
            return response;
        }
    }

    private async Task<AutomationActionTraceEntry> BeginTraceEntryAsync(AutomationCommand command)
    {
        var frame = await TryGetFrameHashAsync();
        var entry = new AutomationActionTraceEntry
        {
            Id = command.Id,
            Method = command.Method,
            Params = command.Params,
            StartedAt = DateTimeOffset.Now,
            FrameIndexBefore = frame.FrameIndex,
            FrameHashBefore = frame.FrameHash
        };

        lock (traceSync)
        {
            actionTrace.Add(entry);
            if (actionTrace.Count > MaxTraceEntries)
            {
                actionTrace.RemoveRange(0, actionTrace.Count - MaxTraceEntries);
            }
        }

        return entry;
    }

    private async Task CompleteTraceEntryAsync(AutomationActionTraceEntry? entry, AutomationResponse response)
    {
        if (entry is null)
        {
            return;
        }

        var frame = await TryGetFrameHashAsync();
        entry.FinishedAt = DateTimeOffset.Now;
        entry.DurationMs = entry.FinishedAt.HasValue ? (int)(entry.FinishedAt.Value - entry.StartedAt).TotalMilliseconds : 0;
        entry.Ok = response.Ok;
        entry.ErrorCode = response.Error?.Code ?? string.Empty;
        entry.ErrorMessage = response.Error?.Message ?? string.Empty;
        entry.FrameIndexAfter = frame.FrameIndex;
        entry.FrameHashAfter = frame.FrameHash;
    }

    private async Task<FrameHashResult> TryGetFrameHashAsync()
    {
        try
        {
            return await automationService.GetFrameHashAsync();
        }
        catch
        {
            return new FrameHashResult();
        }
    }

    private ActionTraceResult ActionTrace(ActionTraceParams parameters)
    {
        List<AutomationActionTraceEntry> entries;
        lock (traceSync)
        {
            entries = actionTrace
                .TakeLast(Math.Max(parameters.Limit ?? actionTrace.Count, 0))
                .Select(CloneTraceEntry)
                .ToList();

            if (parameters.Clear)
            {
                actionTrace.Clear();
            }
        }

        var result = new ActionTraceResult { Entries = entries };
        if (!string.IsNullOrWhiteSpace(parameters.OutputPath))
        {
            var path = Path.GetFullPath(parameters.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(entries, AutomationJson.Options));
            result.Path = path;
        }

        return result;
    }

    private async Task<ReplayLogResult> ReplayLogAsync(ReplayLogParams parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Path))
        {
            throw new AutomationException(AutomationErrorCodes.InvalidRequest, "Replay log path is required.");
        }

        var path = Path.GetFullPath(parameters.Path);
        if (!File.Exists(path))
        {
            throw new AutomationException(AutomationErrorCodes.InvalidRequest, $"Replay log does not exist: {path}");
        }

        var entries = JsonSerializer.Deserialize<List<AutomationActionTraceEntry>>(await File.ReadAllTextAsync(path), AutomationJson.Options) ?? [];
        var result = new ReplayLogResult();
        foreach (var entry in entries.Where(IsReplayable))
        {
            if (parameters.DelayMs > 0)
            {
                await Task.Delay(parameters.DelayMs);
            }

            var command = new AutomationCommand
            {
                Id = $"replay-{Guid.NewGuid():N}",
                Method = entry.Method,
                Params = JsonSerializer.SerializeToElement(entry.Params, AutomationJson.Options)
            };
            var response = await DispatchAsync(command);
            var replayed = CloneTraceEntry(entry);
            replayed.Ok = response.Ok;
            replayed.ErrorCode = response.Error?.Code ?? string.Empty;
            replayed.ErrorMessage = response.Error?.Message ?? string.Empty;
            result.Entries.Add(replayed);
            if (response.Ok)
            {
                result.Replayed++;
            }
            else
            {
                result.Failed++;
            }
        }

        return result;
    }

    private static bool IsReplayable(AutomationActionTraceEntry entry) =>
        entry.Method is "type_text" or "press_key" or "press_button" or "click_screen" or "wait_frame_change" or "wait_stable_frame";

    private static bool IsTraceable(string method) =>
        method is "capture_screen" or "capture_device" or "type_text" or "press_key" or "press_button" or "click_screen" or "wait_frame_change" or "wait_stable_frame";

    private static AutomationActionTraceEntry CloneTraceEntry(AutomationActionTraceEntry entry) => new()
    {
        Id = entry.Id,
        Method = entry.Method,
        Params = entry.Params,
        StartedAt = entry.StartedAt,
        FinishedAt = entry.FinishedAt,
        DurationMs = entry.DurationMs,
        Ok = entry.Ok,
        ErrorCode = entry.ErrorCode,
        ErrorMessage = entry.ErrorMessage,
        FrameIndexBefore = entry.FrameIndexBefore,
        FrameIndexAfter = entry.FrameIndexAfter,
        FrameHashBefore = entry.FrameHashBefore,
        FrameHashAfter = entry.FrameHashAfter
    };

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
