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
    public event EventHandler<AutomationActivityEventArgs>? ActivityChanged;

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
        ActivityChanged?.Invoke(this, AutomationActivityEventArgs.Started(command.Method, command.Id, command.Params, LastCommandAt.Value));
        var response = await DispatchAsync(command);
        LastResponseAt = DateTimeOffset.Now;
        LastResult = response.Ok ? "OK" : "Error";
        LastError = response.Error?.Message ?? string.Empty;
        ActivityChanged?.Invoke(this, AutomationActivityEventArgs.Completed(
            command.Method,
            command.Id,
            LastResult,
            LastError,
            response.Result,
            LastResponseAt.Value));
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
                "get_profiles" => await automationService.GetProfilesAsync(),
                "get_connections" => await automationService.GetConnectionsAsync(),
                "select_device" => await automationService.SelectDeviceAsync(ReadParams<SelectDeviceParams>(command).DeviceId),
                "set_scale" => await automationService.SetScaleAsync(ReadParams<SetScaleParams>(command).Scale),
                "connect_vnc" => await automationService.ConnectVncAsync(ReadParams<ConnectVncParams>(command)),
                "disconnect_vnc" => await automationService.DisconnectVncAsync(),
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
            result.Entries.Add(new ReplayLogEntryResult
            {
                OriginalEntry = CloneTraceEntry(entry),
                ReplayEntry = FindTraceEntry(command.Id) ?? new AutomationActionTraceEntry
                {
                    Id = command.Id,
                    Method = command.Method,
                    Ok = response.Ok,
                    ErrorCode = response.Error?.Code ?? string.Empty,
                    ErrorMessage = response.Error?.Message ?? string.Empty
                }
            });
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
        method is "select_device" or "set_scale" or "connect_vnc" or "disconnect_vnc" or
            "capture_screen" or "capture_device" or "type_text" or "press_key" or "press_button" or "click_screen" or "wait_frame_change" or "wait_stable_frame";

    private AutomationActionTraceEntry? FindTraceEntry(string id)
    {
        lock (traceSync)
        {
            return actionTrace.LastOrDefault(entry => entry.Id.Equals(id, StringComparison.Ordinal)) is { } entry
                ? CloneTraceEntry(entry)
                : null;
        }
    }

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

public sealed class AutomationActivityEventArgs : EventArgs
{
    private AutomationActivityEventArgs(
        string method,
        string commandId,
        JsonElement? parameters,
        string state,
        string result,
        string error,
        object? responseResult,
        DateTimeOffset timestamp)
    {
        Method = method;
        CommandId = commandId;
        Parameters = parameters;
        State = state;
        Result = result;
        Error = error;
        ResponseResult = responseResult;
        Timestamp = timestamp;
    }

    public string Method { get; }
    public string CommandId { get; }
    public JsonElement? Parameters { get; }
    public string State { get; }
    public string Result { get; }
    public string Error { get; }
    public object? ResponseResult { get; }
    public DateTimeOffset Timestamp { get; }

    public string DisplayText
    {
        get
        {
            var time = Timestamp.ToString("HH:mm:ss");
            if (State == "started")
            {
                return $"{time} AI -> {FormatRequest(Method, Parameters)}";
            }

            return string.IsNullOrWhiteSpace(Error)
                ? $"{time} Sim <- {FormatResponse(Method, ResponseResult)}"
                : $"{time} Sim <- {Method}: {Result} - {Error}";
        }
    }

    public static AutomationActivityEventArgs Started(string method, string commandId, JsonElement? parameters, DateTimeOffset timestamp) =>
        new(method, commandId, parameters, "started", string.Empty, string.Empty, null, timestamp);

    public static AutomationActivityEventArgs Completed(string method, string commandId, string result, string error, object? responseResult, DateTimeOffset timestamp) =>
        new(method, commandId, null, "completed", result, error, responseResult, timestamp);

    private static string FormatRequest(string method, JsonElement? parameters)
    {
        return method switch
        {
            "type_text" => $"type text {Quoted(ReadString(parameters, "text"))}",
            "press_key" => $"press key {ReadString(parameters, "key")}",
            "press_button" => $"press button {ReadString(parameters, "buttonId")}",
            "click_screen" => $"click screen ({ReadInt(parameters, "x")}, {ReadInt(parameters, "y")}) {ReadString(parameters, "button", "left")}",
            "wait_stable_frame" => $"wait stable frame {ReadInt(parameters, "quietMs", 300)}ms",
            "wait_frame_change" => $"wait frame change timeout {ReadInt(parameters, "timeoutMs", 3000)}ms",
            "capture_screen" => $"capture screen {PathHint(ReadString(parameters, "outputPath"))}",
            "capture_device" => $"capture device {PathHint(ReadString(parameters, "outputPath"))}",
            "select_device" => $"select device {ReadString(parameters, "deviceId")}",
            "set_scale" => $"set scale {ReadDouble(parameters, "scale", 1):0.##}",
            "connect_vnc" => FormatConnectRequest(parameters),
            "disconnect_vnc" => "disconnect VNC",
            "get_state" => "inspect state",
            "frame_hash" => "read frame hash",
            "get_profiles" => "list device profiles",
            "get_connections" => "list saved connections",
            "action_trace" => "read action trace",
            "replay_log" => $"replay trace {PathHint(ReadString(parameters, "path"))}",
            _ => method
        };
    }

    private static string FormatResponse(string method, object? responseResult)
    {
        var json = ToJsonElement(responseResult);
        return method switch
        {
            "type_text" => "typed text",
            "press_key" => "sent key",
            "press_button" => "pressed device button",
            "click_screen" => "sent pointer click",
            "wait_stable_frame" => $"stable={ReadBool(json, "stable")} frame={ReadLong(json, "frameIndex")}",
            "wait_frame_change" => $"changed={ReadBool(json, "changed")} frame={ReadLong(json, "currentFrameIndex")}",
            "capture_screen" => $"screen saved {PathHint(ReadString(json, "path"))}",
            "capture_device" => $"device saved {PathHint(ReadString(json, "path"))}",
            "select_device" or "set_scale" or "connect_vnc" or "disconnect_vnc" => ReadString(json, "message", "OK"),
            "get_state" => $"state {ReadString(json, "deviceId")} connected={ReadBool(json, "connected")}",
            "frame_hash" => $"hash {Short(ReadString(json, "frameHash"))} frame={ReadLong(json, "frameIndex")}",
            "get_profiles" => $"profiles listed ({CountArray(json, "profiles")})",
            "get_connections" => $"connections listed ({CountArray(json, "connections")})",
            "action_trace" => $"trace entries={CountArray(json, "entries")}",
            "replay_log" => $"replayed={ReadInt(json, "replayed")} failed={ReadInt(json, "failed")}",
            _ => "OK"
        };
    }

    private static string FormatConnectRequest(JsonElement? parameters)
    {
        var profile = ReadString(parameters, "profileId");
        if (!string.IsNullOrWhiteSpace(profile))
        {
            return $"connect VNC profile {profile}";
        }

        return $"connect VNC {ReadString(parameters, "host")}:{ReadInt(parameters, "port")} device={ReadString(parameters, "deviceId")}";
    }

    private static JsonElement? ToJsonElement(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(value, AutomationJson.Options);
    }

    private static string ReadString(JsonElement? element, string name, string fallback = "")
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt(JsonElement? element, string name, int fallback = 0)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.TryGetInt32(out var parsed)
            ? parsed
            : fallback;
    }

    private static long ReadLong(JsonElement? element, string name, long fallback = 0)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.TryGetInt64(out var parsed)
            ? parsed
            : fallback;
    }

    private static double ReadDouble(JsonElement? element, string name, double fallback = 0)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.TryGetDouble(out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBool(JsonElement? element, string name)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               property.GetBoolean();
    }

    private static int CountArray(JsonElement? element, string name)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }

    private static string Quoted(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var normalized = value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
        return normalized.Length > 24 ? $"\"{normalized[..24]}...\"" : $"\"{normalized}\"";
    }

    private static string PathHint(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default path" : Path.GetFileName(value);
    }

    private static string Short(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value[..Math.Min(value.Length, 8)];
    }
}
