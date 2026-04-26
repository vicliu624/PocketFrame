namespace PocketFrame.Environments;

public sealed class EnvironmentService : IEnvironmentService
{
    private readonly EnvironmentProfileService profileService;
    private readonly ProcessRunner processRunner;

    public EnvironmentService()
        : this(new EnvironmentProfileService(), new ProcessRunner())
    {
    }

    internal EnvironmentService(EnvironmentProfileService profileService, ProcessRunner processRunner)
    {
        this.profileService = profileService;
        this.processRunner = processRunner;
    }

    public Task<EnvironmentProfilesResult> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        profileService.ListAsync(cancellationToken: cancellationToken);

    public async Task<EnvironmentStateResult> GetStateAsync(string profileId = "", CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(profileId, cancellationToken);
        var command = await ExecuteProfileCommandAsync(profile, "command -v vncserver >/dev/null && echo vncserver-ok || echo vncserver-missing; pgrep -fa 'Xtigervnc|Xvnc|vncserver' || true", string.Empty, 10000, string.Empty, cancellationToken);
        var running = command.Stdout.Contains("Xtigervnc", StringComparison.OrdinalIgnoreCase) ||
                      command.Stdout.Contains("Xvnc", StringComparison.OrdinalIgnoreCase) ||
                      command.Stdout.Contains(profile.Vnc.Display, StringComparison.Ordinal);
        return new EnvironmentStateResult
        {
            ProfileId = profile.Id,
            Type = profile.Type,
            Available = command.ExitCode == 0 && !command.TimedOut,
            Message = command.Stdout.Contains("vncserver-ok", StringComparison.Ordinal) ? "Environment available." : "vncserver command is missing.",
            WorkingDirectory = profile.WorkingDirectory,
            Vnc = new EnvironmentVncState
            {
                Display = profile.Vnc.Display,
                Host = profile.Vnc.Host,
                Port = profile.Vnc.Port,
                Geometry = profile.Vnc.Geometry,
                Depth = profile.Vnc.Depth,
                Running = running,
                Pid = FirstPid(command.Stdout)
            }
        };
    }

    public async Task<EnvironmentCommandResult> ExecuteAsync(EnvironmentCommandParams parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Command))
        {
            throw new InvalidOperationException("Environment command is required.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        return await ExecuteProfileCommandAsync(
            profile,
            parameters.Command,
            parameters.WorkingDirectory,
            parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs,
            parameters.Stdin,
            cancellationToken);
    }

    public async Task<EnvironmentOperationResult> StartVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var display = string.IsNullOrWhiteSpace(parameters.Display) ? profile.Vnc.Display : parameters.Display;
        var geometry = string.IsNullOrWhiteSpace(parameters.Geometry) ? profile.Vnc.Geometry : parameters.Geometry;
        var depth = parameters.Depth <= 0 ? profile.Vnc.Depth : parameters.Depth;
        var command = $"vncserver {ShellQuote(display)} -geometry {ShellQuote(geometry)} -depth {depth}";
        var result = await ExecuteProfileCommandAsync(profile, command, string.Empty, Timeout(parameters, profile), string.Empty, cancellationToken);
        return Operation(result.ExitCode == 0, result.ExitCode == 0 ? $"VNC started on {display} ({geometry})." : $"Failed to start VNC on {display}.", result);
    }

    public async Task<EnvironmentOperationResult> StopVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var display = string.IsNullOrWhiteSpace(parameters.Display) ? profile.Vnc.Display : parameters.Display;
        var result = await ExecuteProfileCommandAsync(profile, $"vncserver -kill {ShellQuote(display)}", string.Empty, Timeout(parameters, profile), string.Empty, cancellationToken);
        var ok = result.ExitCode == 0 || result.Stderr.Contains("not running", StringComparison.OrdinalIgnoreCase) || result.Stdout.Contains("not running", StringComparison.OrdinalIgnoreCase);
        return Operation(ok, ok ? $"VNC stopped on {display}." : $"Failed to stop VNC on {display}.", result);
    }

    public async Task<EnvironmentOperationResult> RestartVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default)
    {
        await StopVncAsync(parameters, cancellationToken);
        return await StartVncAsync(parameters, cancellationToken);
    }

    public async Task<EnvironmentProcessesResult> GetProcessesAsync(EnvironmentProcessQueryParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var result = await ExecuteProfileCommandAsync(profile, "ps -eo pid=,comm=,args=", string.Empty, profile.CommandTimeoutMs, string.Empty, cancellationToken);
        var processes = ParseProcesses(result.Stdout);
        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            processes = processes
                .Where(process => process.Command.Contains(parameters.Filter, StringComparison.OrdinalIgnoreCase) ||
                                  process.Args.Contains(parameters.Filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new EnvironmentProcessesResult { Processes = processes };
    }

    public async Task<EnvironmentOperationResult> KillProcessAsync(EnvironmentKillProcessParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var signal = NormalizeSignal(parameters.Signal);
        var command = parameters.Pid.HasValue
            ? $"kill -s {ShellQuote(signal)} {parameters.Pid.Value}"
            : !string.IsNullOrWhiteSpace(parameters.Match)
                ? $"pkill -{ShellQuote(signal)} -f {ShellQuote(parameters.Match)}"
                : throw new InvalidOperationException("Kill process requires pid or match.");
        var result = await ExecuteProfileCommandAsync(profile, command, string.Empty, parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs, string.Empty, cancellationToken);
        return Operation(result.ExitCode == 0, result.ExitCode == 0 ? "Process signal sent." : "Process signal failed.", result);
    }

    public async Task<EnvironmentFileResult> ReadFileAsync(EnvironmentFileParams parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Path))
        {
            throw new InvalidOperationException("Read file path is required.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var result = await ExecuteProfileCommandAsync(profile, $"cat {ShellQuote(parameters.Path)}", string.Empty, Timeout(parameters, profile), string.Empty, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new IOException(result.Stderr);
        }

        return new EnvironmentFileResult
        {
            Path = parameters.Path,
            Content = result.Stdout,
            Bytes = System.Text.Encoding.UTF8.GetByteCount(result.Stdout)
        };
    }

    public async Task<EnvironmentOperationResult> WriteFileAsync(EnvironmentFileParams parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Path))
        {
            throw new InvalidOperationException("Write file path is required.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var command = $"mkdir -p {ShellQuote(Path.GetDirectoryName(parameters.Path.Replace('\\', '/')) ?? ".")} && cat > {ShellQuote(parameters.Path)}";
        var result = await ExecuteProfileCommandAsync(profile, command, string.Empty, Timeout(parameters, profile), parameters.Content, cancellationToken);
        return Operation(result.ExitCode == 0, result.ExitCode == 0 ? $"File written: {parameters.Path}" : $"Failed to write file: {parameters.Path}", result);
    }

    public async Task<EnvironmentOperationResult> InstallPackagesAsync(EnvironmentInstallPackagesParams parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.Packages.Count == 0 || parameters.Packages.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Install packages requires at least one package name.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var sudo = parameters.Sudo ? "sudo " : string.Empty;
        var packages = string.Join(' ', parameters.Packages.Select(ShellQuote));
        var command = parameters.Update
            ? $"{sudo}apt-get update && {sudo}apt-get install -y {packages}"
            : $"{sudo}apt-get install -y {packages}";
        var result = await ExecuteProfileCommandAsync(profile, command, string.Empty, parameters.TimeoutMs <= 0 ? 120000 : parameters.TimeoutMs, string.Empty, cancellationToken);
        return Operation(result.ExitCode == 0, result.ExitCode == 0 ? $"Packages installed: {string.Join(", ", parameters.Packages)}" : "Package installation failed.", result);
    }

    public async Task<EnvironmentLaunchResult> LaunchAsync(EnvironmentLaunchParams parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Command))
        {
            throw new InvalidOperationException("Launch command is required.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var workingDirectory = string.IsNullOrWhiteSpace(parameters.WorkingDirectory) ? profile.WorkingDirectory : parameters.WorkingDirectory;
        var logPath = string.IsNullOrWhiteSpace(parameters.LogPath)
            ? $"~/pocketframe-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log"
            : parameters.LogPath;
        var exports = string.Join(' ', parameters.Env.Select(pair => $"{pair.Key}={ShellQuote(pair.Value)}"));
        var command = $"mkdir -p $(dirname {ShellQuote(logPath)}) && cd {ShellQuote(workingDirectory)} && nohup {exports} {profile.Shell} -lc {ShellQuote(parameters.Command)} > {ShellQuote(logPath)} 2>&1 & echo $!";
        var result = await ExecuteProfileCommandAsync(profile, command, workingDirectory, parameters.TimeoutMs <= 0 ? 10000 : parameters.TimeoutMs, string.Empty, cancellationToken);
        if (result.ExitCode != 0 || !int.TryParse(result.Stdout.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), out var pid))
        {
            throw new IOException(string.IsNullOrWhiteSpace(result.Stderr) ? "Launch command did not return a pid." : result.Stderr);
        }

        return new EnvironmentLaunchResult
        {
            ProfileId = profile.Id,
            Pid = pid,
            Command = parameters.Command,
            WorkingDirectory = workingDirectory,
            LogPath = logPath
        };
    }

    public async Task<EnvironmentFileResult> TailFileAsync(EnvironmentTailFileParams parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parameters.Path))
        {
            throw new InvalidOperationException("Tail file path is required.");
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var lines = parameters.Lines <= 0 ? 80 : parameters.Lines;
        var result = await ExecuteProfileCommandAsync(profile, $"tail -n {lines} {ShellQuote(parameters.Path)}", string.Empty, parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs, string.Empty, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new IOException(result.Stderr);
        }

        return new EnvironmentFileResult
        {
            Path = parameters.Path,
            Content = result.Stdout,
            Bytes = System.Text.Encoding.UTF8.GetByteCount(result.Stdout)
        };
    }

    public async Task<EnvironmentAppStatusResult> GetAppStatusAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var match = !string.IsNullOrWhiteSpace(parameters.ProcessMatch)
            ? parameters.ProcessMatch
            : !string.IsNullOrWhiteSpace(parameters.Id)
                ? parameters.Id
                : !string.IsNullOrWhiteSpace(parameters.BinaryPath)
                    ? parameters.BinaryPath
                    : parameters.Command;
        var binary = parameters.BinaryPath;
        var pidLookup = string.IsNullOrWhiteSpace(match)
            ? "pid=''"
            : $"pid=$(pgrep -f {ShellQuote(match)} | head -n1 || true)";
        var command = $"{pidLookup}; if [ -n \"$pid\" ]; then echo PID=$pid; echo CWD=$(readlink /proc/$pid/cwd 2>/dev/null || true); tr '\\0' ' ' < /proc/$pid/cmdline 2>/dev/null | sed 's/^/CMD=/'; echo; fi; if [ -n {ShellQuote(binary)} ]; then stat -c 'BIN_MTIME=%y' {ShellQuote(binary)} 2>/dev/null || true; fi";
        var result = await ExecuteProfileCommandAsync(profile, command, parameters.WorkingDirectory, parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs, string.Empty, cancellationToken);
        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pidLine = lines.FirstOrDefault(line => line.StartsWith("PID=", StringComparison.Ordinal));
        int.TryParse(pidLine?["PID=".Length..], out var pid);
        return new EnvironmentAppStatusResult
        {
            ProfileId = profile.Id,
            Id = parameters.Id,
            Running = pid > 0,
            Pid = pid > 0 ? pid : null,
            Cwd = Value(lines, "CWD="),
            Command = Value(lines, "CMD="),
            BinaryPath = binary,
            BinaryMtime = Value(lines, "BIN_MTIME="),
            LogPath = parameters.LogPath,
            StateDir = parameters.Env.TryGetValue("XDG_STATE_HOME", out var stateDir) ? stateDir : string.Empty
        };
    }

    public async Task<EnvironmentOperationResult> KillAppAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default) =>
        await KillProcessAsync(new EnvironmentKillProcessParams { ProfileId = parameters.ProfileId, Match = !string.IsNullOrWhiteSpace(parameters.ProcessMatch) ? parameters.ProcessMatch : parameters.Id, Signal = "TERM", TimeoutMs = parameters.TimeoutMs }, cancellationToken);

    public async Task<EnvironmentLaunchResult> LaunchAppAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.KillBeforeLaunch)
        {
            await KillAppAsync(parameters, cancellationToken);
        }

        if (parameters.ClearPaths.Count > 0)
        {
            await CleanAppStateAsync(parameters, cancellationToken);
        }

        return await LaunchAsync(new EnvironmentLaunchParams { ProfileId = parameters.ProfileId, Command = parameters.Command, WorkingDirectory = parameters.WorkingDirectory, LogPath = parameters.LogPath, Env = parameters.Env, TimeoutMs = parameters.TimeoutMs }, cancellationToken);
    }

    public async Task<EnvironmentOperationResult> CleanAppStateAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.ClearPaths.Count == 0)
        {
            return new EnvironmentOperationResult { Ok = true, Message = "No app state paths configured." };
        }

        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var paths = string.Join(' ', parameters.ClearPaths.Select(ShellQuote));
        var result = await ExecuteProfileCommandAsync(profile, $"rm -rf -- {paths}", parameters.WorkingDirectory, parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs, string.Empty, cancellationToken);
        return Operation(result.ExitCode == 0, result.ExitCode == 0 ? "App state cleaned." : "Failed to clean app state.", result);
    }

    public Task<EnvironmentFileResult> TailAppLogAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default) =>
        TailFileAsync(new EnvironmentTailFileParams { ProfileId = parameters.ProfileId, Path = parameters.LogPath, Lines = 120, TimeoutMs = parameters.TimeoutMs }, cancellationToken);

    public async Task<EnvironmentInputDevicesResult> GetInputDevicesAsync(EnvironmentCommandParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var command = "for f in /dev/input/event*; do [ -e \"$f\" ] || continue; name=$(cat /sys/class/input/$(basename $f)/device/name 2>/dev/null || true); echo \"$f|$name\"; done";
        var result = await ExecuteProfileCommandAsync(profile, command, parameters.WorkingDirectory, parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs, string.Empty, cancellationToken);
        return new EnvironmentInputDevicesResult
        {
            Devices = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('|', 2))
                .Select(parts => new EnvironmentInputDevice { Path = parts[0], Name = parts.Length > 1 ? parts[1] : string.Empty })
                .ToList()
        };
    }

    public async Task<EnvironmentEvdevCaptureResult> CaptureEvdevAsync(EnvironmentEvdevCaptureParams parameters, CancellationToken cancellationToken = default)
    {
        var profile = await ResolveAsync(parameters.ProfileId, cancellationToken);
        var duration = Math.Max(1, parameters.DurationMs) / 1000.0;
        var command = $"timeout {duration:0.###}s evtest {ShellQuote(parameters.Device)} 2>&1 || true";
        var result = await ExecuteProfileCommandAsync(profile, command, string.Empty, parameters.TimeoutMs <= 0 ? Math.Max(parameters.DurationMs + 2000, 5000) : parameters.TimeoutMs, string.Empty, cancellationToken);
        return new EnvironmentEvdevCaptureResult
        {
            Device = parameters.Device,
            Events = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Contains("EV_KEY", StringComparison.OrdinalIgnoreCase) || line.Contains("KEY_", StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    private async Task<EnvironmentProfile> ResolveAsync(string profileId, CancellationToken cancellationToken) =>
        await profileService.ResolveAsync(profileId, cancellationToken: cancellationToken);

    private Task<EnvironmentCommandResult> ExecuteProfileCommandAsync(
        EnvironmentProfile profile,
        string command,
        string workingDirectory,
        int timeoutMs,
        string stdin,
        CancellationToken cancellationToken)
    {
        var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? profile.WorkingDirectory : workingDirectory;
        return profile.Type.Equals(EnvironmentProfileTypes.Wsl, StringComparison.OrdinalIgnoreCase)
            ? RunWslAsync(profile, command, resolvedWorkingDirectory, stdin, timeoutMs, cancellationToken)
            : RunLocalAsync(profile, command, resolvedWorkingDirectory, stdin, timeoutMs, cancellationToken);
    }

    private async Task<EnvironmentCommandResult> RunWslAsync(EnvironmentProfile profile, string command, string workingDirectory, string stdin, int timeoutMs, CancellationToken cancellationToken)
    {
        var args = $"-d {QuoteArgument(profile.Distro)} --cd {QuoteArgument(workingDirectory)} -- {profile.Shell} -lc {QuoteArgument(command)}";
        var result = await processRunner.RunAsync("wsl.exe", args, command, workingDirectory, stdin, timeoutMs, cancellationToken);
        result.ProfileId = profile.Id;
        return result;
    }

    private async Task<EnvironmentCommandResult> RunLocalAsync(EnvironmentProfile profile, string command, string workingDirectory, string stdin, int timeoutMs, CancellationToken cancellationToken)
    {
        var shell = OperatingSystem.IsWindows() ? "powershell.exe" : profile.Shell;
        var args = OperatingSystem.IsWindows()
            ? $"-NoProfile -ExecutionPolicy Bypass -Command {QuoteArgument(command)}"
            : $"-lc {QuoteArgument(command)}";
        var result = await processRunner.RunAsync(shell, args, command, workingDirectory, stdin, timeoutMs, cancellationToken);
        result.ProfileId = profile.Id;
        return result;
    }

    private static List<EnvironmentProcessInfo> ParseProcesses(string text)
    {
        var processes = new List<EnvironmentProcessInfo>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out var pid))
            {
                processes.Add(new EnvironmentProcessInfo
                {
                    Pid = pid,
                    Command = parts[1],
                    Args = parts.Length == 3 ? parts[2] : string.Empty
                });
            }
        }

        return processes;
    }

    private static int? FirstPid(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out var pid))
            {
                return pid;
            }
        }

        return null;
    }

    private static string Value(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? string.Empty;

    private static int Timeout(EnvironmentVncParams parameters, EnvironmentProfile profile) =>
        parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs;

    private static int Timeout(EnvironmentFileParams parameters, EnvironmentProfile profile) =>
        parameters.TimeoutMs <= 0 ? profile.CommandTimeoutMs : parameters.TimeoutMs;

    private static EnvironmentOperationResult Operation(bool ok, string message, EnvironmentCommandResult command) => new()
    {
        Ok = ok,
        Message = message,
        Command = command
    };

    private static string NormalizeSignal(string signal) =>
        string.IsNullOrWhiteSpace(signal) ? "TERM" : signal.Trim().TrimStart('-').ToUpperInvariant();

    public static string ShellQuote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    private static string QuoteArgument(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
