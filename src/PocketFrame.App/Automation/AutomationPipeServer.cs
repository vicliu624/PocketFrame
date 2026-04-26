using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PocketFrame.App.Utils;
using PocketFrame.Automation;
using PocketFrame.Environments;

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
                "get_input_model" => await automationService.GetInputModelAsync(),
                "get_keyboard_state" => await automationService.GetKeyboardStateAsync(),
                "get_profiles" => await automationService.GetProfilesAsync(),
                "get_connections" => await automationService.GetConnectionsAsync(),
                "select_device" => await automationService.SelectDeviceAsync(ReadParams<SelectDeviceParams>(command).DeviceId),
                "set_scale" => await automationService.SetScaleAsync(ReadParams<SetScaleParams>(command).Scale),
                "connect_vnc" => await automationService.ConnectVncAsync(ReadParams<ConnectVncParams>(command)),
                "disconnect_vnc" => await automationService.DisconnectVncAsync(),
                "environment_profiles" => await automationService.GetEnvironmentProfilesAsync(),
                "environment_get_state" => await automationService.GetEnvironmentStateAsync(ReadProfileId(command)),
                "environment_exec" => await automationService.ExecuteEnvironmentCommandAsync(ReadParams<EnvironmentCommandParams>(command)),
                "environment_start_vnc" => await automationService.StartEnvironmentVncAsync(ReadParams<EnvironmentVncParams>(command)),
                "environment_stop_vnc" => await automationService.StopEnvironmentVncAsync(ReadParams<EnvironmentVncParams>(command)),
                "environment_restart_vnc" => await automationService.RestartEnvironmentVncAsync(ReadParams<EnvironmentVncParams>(command)),
                "environment_processes" => await automationService.GetEnvironmentProcessesAsync(ReadParams<EnvironmentProcessQueryParams>(command)),
                "environment_kill_process" => await automationService.KillEnvironmentProcessAsync(ReadParams<EnvironmentKillProcessParams>(command)),
                "environment_read_file" => await automationService.ReadEnvironmentFileAsync(ReadParams<EnvironmentFileParams>(command)),
                "environment_write_file" => await automationService.WriteEnvironmentFileAsync(ReadParams<EnvironmentFileParams>(command)),
                "environment_install_packages" => await automationService.InstallEnvironmentPackagesAsync(ReadParams<EnvironmentInstallPackagesParams>(command)),
                "environment_launch" => await automationService.LaunchEnvironmentProcessAsync(ReadParams<EnvironmentLaunchParams>(command)),
                "environment_tail_file" => await automationService.TailEnvironmentFileAsync(ReadParams<EnvironmentTailFileParams>(command)),
                "environment_app_status" => await automationService.GetEnvironmentAppStatusAsync(ReadParams<EnvironmentAppParams>(command)),
                "environment_app_kill" => await automationService.KillEnvironmentAppAsync(ReadParams<EnvironmentAppParams>(command)),
                "environment_app_launch" => await automationService.LaunchEnvironmentAppAsync(ReadParams<EnvironmentAppParams>(command)),
                "environment_app_clean_state" => await automationService.CleanEnvironmentAppStateAsync(ReadParams<EnvironmentAppParams>(command)),
                "environment_app_tail_log" => await automationService.TailEnvironmentAppLogAsync(ReadParams<EnvironmentAppParams>(command)),
                "environment_input_devices" => await automationService.GetEnvironmentInputDevicesAsync(ReadParams<EnvironmentCommandParams>(command)),
                "environment_evdev_capture" => await automationService.CaptureEnvironmentEvdevAsync(ReadParams<EnvironmentEvdevCaptureParams>(command)),
                "frame_hash" => await automationService.GetFrameHashAsync(),
                "capture_screen" => await automationService.CaptureScreenAsync(ReadParams<CaptureParams>(command).OutputPath),
                "capture_device" => await automationService.CaptureDeviceAsync(ReadParams<CaptureParams>(command).OutputPath),
                "type_text" => await RunAsync(async () => await automationService.TypeTextAsync(ReadParams<TextInputParams>(command).Text)),
                "press_key" => await automationService.PressKeyAsync(ReadParams<KeyPressParams>(command).Key),
                "press_button" => await automationService.PressButtonAsync(ReadParams<ButtonPressParams>(command)),
                "click_screen" => await RunAsync(async () =>
                {
                    var parameters = ReadParams<ClickScreenParams>(command);
                    await automationService.ClickScreenAsync(parameters.X, parameters.Y, parameters.Button);
                }),
                "wait" => await automationService.WaitAsync(ReadParams<WaitParams>(command).DurationMs),
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
        catch (InvalidOperationException ex)
        {
            InputDiagnostics.Write("Automation", $"Command invalid {command.Method}: {ex.Message}");
            var response = AutomationResponse.Failure(command.Id, AutomationErrorCodes.InvalidRequest, ex.Message);
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
        entry.Result = response.Result;
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
        entry.Method is "type_text" or "press_key" or "press_button" or "click_screen" or "wait" or "wait_frame_change" or "wait_stable_frame";

    private static bool IsTraceable(string method) =>
        method is "select_device" or "set_scale" or "connect_vnc" or "disconnect_vnc" or
            "environment_exec" or "environment_start_vnc" or "environment_stop_vnc" or "environment_restart_vnc" or
            "environment_kill_process" or "environment_read_file" or "environment_write_file" or
            "environment_install_packages" or "environment_launch" or "environment_tail_file" or
            "environment_app_status" or "environment_app_kill" or "environment_app_launch" or "environment_app_clean_state" or "environment_app_tail_log" or
            "environment_input_devices" or "environment_evdev_capture" or
            "capture_screen" or "capture_device" or "type_text" or "press_key" or "press_button" or "click_screen" or "wait" or "wait_frame_change" or "wait_stable_frame";

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
        Result = entry.Result,
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

    private static string ReadProfileId(AutomationCommand command) =>
        ReadParams<EnvironmentCommandParams>(command).ProfileId;

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

            var responder = Method.StartsWith("environment_", StringComparison.Ordinal) ? "Env" : "Sim";
            return string.IsNullOrWhiteSpace(Error)
                ? $"{time} {responder} <- {FormatResponse(Method, ResponseResult)}"
                : $"{time} {responder} <- {Method}: {Result} - {Error}";
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
            "press_button" => FormatPressButtonRequest(parameters),
            "click_screen" => $"click screen ({ReadInt(parameters, "x")}, {ReadInt(parameters, "y")}) {ReadString(parameters, "button", "left")}",
            "wait" => $"wait {ReadInt(parameters, "durationMs", 1000)}ms",
            "wait_stable_frame" => $"wait stable frame {ReadInt(parameters, "quietMs", 300)}ms",
            "wait_frame_change" => $"wait frame change timeout {ReadInt(parameters, "timeoutMs", 3000)}ms",
            "capture_screen" => $"capture screen {PathHint(ReadString(parameters, "outputPath"))}",
            "capture_device" => $"capture device {PathHint(ReadString(parameters, "outputPath"))}",
            "select_device" => $"select device {ReadString(parameters, "deviceId")}",
            "set_scale" => $"set scale {ReadDouble(parameters, "scale", 1):0.##}",
            "connect_vnc" => FormatConnectRequest(parameters),
            "disconnect_vnc" => "disconnect VNC",
            "get_state" => "inspect state",
            "get_input_model" => "inspect input model",
            "get_keyboard_state" => "inspect keyboard state",
            "frame_hash" => "read frame hash",
            "get_profiles" => "list device profiles",
            "get_connections" => "list saved connections",
            "environment_profiles" => "list target environments",
            "environment_get_state" => $"inspect environment {ReadString(parameters, "profileId", "default")}",
            "environment_exec" => $"env exec {Quoted(ReadString(parameters, "command"))}",
            "environment_start_vnc" => $"start env VNC {ReadString(parameters, "display", "profile display")} {ReadString(parameters, "geometry", "profile geometry")}",
            "environment_stop_vnc" => $"stop env VNC {ReadString(parameters, "display", "profile display")}",
            "environment_restart_vnc" => $"restart env VNC {ReadString(parameters, "display", "profile display")} {ReadString(parameters, "geometry", "profile geometry")}",
            "environment_processes" => $"list env processes {ReadString(parameters, "filter")}",
            "environment_kill_process" => $"kill env process {ReadKillTarget(parameters)}",
            "environment_read_file" => $"read env file {PathHint(ReadString(parameters, "path"))}",
            "environment_write_file" => $"write env file {PathHint(ReadString(parameters, "path"))}",
            "environment_install_packages" => $"install env packages {ReadArrayPreview(parameters, "packages")}",
            "environment_launch" => $"launch env app {Quoted(ReadString(parameters, "command"))}",
            "environment_tail_file" => $"tail env file {PathHint(ReadString(parameters, "path"))}",
            "environment_app_status" => $"app status {ReadString(parameters, "id", ReadString(parameters, "processMatch"))}",
            "environment_app_kill" => $"app kill {ReadString(parameters, "id", ReadString(parameters, "processMatch"))}",
            "environment_app_launch" => $"app launch {Quoted(ReadString(parameters, "command"))}",
            "environment_app_clean_state" => $"app clean state {ReadArrayPreview(parameters, "clearPaths")}",
            "environment_app_tail_log" => $"app tail log {PathHint(ReadString(parameters, "logPath"))}",
            "environment_input_devices" => "list input devices",
            "environment_evdev_capture" => $"capture evdev {ReadString(parameters, "device")}",
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
            "press_key" => FormatInputResult(json),
            "press_button" => FormatInputResult(json),
            "click_screen" => "sent pointer click",
            "wait" => $"waited {ReadInt(json, "waitedMs")}ms frame={ReadLong(json, "frameIndex")}",
            "wait_stable_frame" => $"stable={ReadBool(json, "stable")} frame={ReadLong(json, "frameIndex")}",
            "wait_frame_change" => $"changed={ReadBool(json, "changed")} frame={ReadLong(json, "currentFrameIndex")}",
            "capture_screen" => $"screen saved {PathHint(ReadString(json, "path"))}",
            "capture_device" => $"device saved {PathHint(ReadString(json, "path"))}",
            "select_device" or "set_scale" or "connect_vnc" or "disconnect_vnc" => ReadString(json, "message", "OK"),
            "get_state" => $"state {ReadString(json, "deviceId")} connected={ReadBool(json, "connected")}",
            "get_input_model" => $"input model keys={CountArray(json, "keys")}",
            "get_keyboard_state" => $"layers active={CountArray(json, "activeLayers")}",
            "frame_hash" => $"hash {Short(ReadString(json, "frameHash"))} frame={ReadLong(json, "frameIndex")}",
            "get_profiles" => $"profiles listed ({CountArray(json, "profiles")})",
            "get_connections" => $"connections listed ({CountArray(json, "connections")})",
            "environment_profiles" => $"environments listed ({CountArray(json, "profiles")})",
            "environment_get_state" => $"env available={ReadBool(json, "available")} vnc={ReadNestedBool(json, "vnc", "running")}",
            "environment_exec" => $"env exit={ReadInt(json, "exitCode")} {ReadInt(json, "durationMs")}ms",
            "environment_start_vnc" or "environment_stop_vnc" or "environment_restart_vnc" or "environment_kill_process" or "environment_write_file" => ReadString(json, "message", "OK"),
            "environment_install_packages" => ReadString(json, "message", "OK"),
            "environment_launch" => $"launched pid={ReadInt(json, "pid")} log={PathHint(ReadString(json, "logPath"))}",
            "environment_app_status" => $"app running={ReadBool(json, "running")} pid={ReadInt(json, "pid")}",
            "environment_app_launch" => $"app launched pid={ReadInt(json, "pid")} log={PathHint(ReadString(json, "logPath"))}",
            "environment_app_kill" or "environment_app_clean_state" => ReadString(json, "message", "OK"),
            "environment_app_tail_log" => $"app log {PathHint(ReadString(json, "path"))}",
            "environment_input_devices" => $"input devices={CountArray(json, "devices")}",
            "environment_evdev_capture" => $"evdev events={CountArray(json, "events")}",
            "environment_processes" => $"processes listed ({CountArray(json, "processes")})",
            "environment_read_file" => $"file read {PathHint(ReadString(json, "path"))}",
            "environment_tail_file" => $"file tailed {PathHint(ReadString(json, "path"))}",
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

    private static string FormatInputResult(JsonElement? json)
    {
        var layer = ReadString(json, "inputLayer");
        var button = ReadString(json, "requestedButtonId");
        var key = ReadString(json, "requestedKey");
        var resolved = ReadString(json, "resolvedKey");
        var emitted = ReadString(json, "emittedKey");
        return string.IsNullOrWhiteSpace(button)
            ? $"{layer} {key} => VNC {emitted}"
            : $"{layer} {button} => {resolved} => VNC {emitted}";
    }

    private static string FormatPressButtonRequest(JsonElement? parameters)
    {
        var buttonId = ReadString(parameters, "buttonId");
        var durationMs = ReadInt(parameters, "durationMs");
        return durationMs > 0 ? $"hold button {buttonId} {durationMs}ms" : $"press button {buttonId}";
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

    private static bool ReadNestedBool(JsonElement? element, string objectName, string name)
    {
        return element is { ValueKind: JsonValueKind.Object } value &&
               value.TryGetProperty(objectName, out var nested) &&
               ReadBool(nested, name);
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

    private static string ReadKillTarget(JsonElement? parameters)
    {
        var pid = ReadInt(parameters, "pid");
        return pid > 0 ? $"pid={pid}" : ReadString(parameters, "match");
    }

    private static string ReadArrayPreview(JsonElement? element, string name)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var items = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToList();
        return string.Join(", ", items);
    }
}
