using System.Text.Json.Serialization;

namespace PocketFrame.Environments;

public sealed class EnvironmentProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "default-wsl";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default WSL";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "wsl";

    [JsonPropertyName("distro")]
    public string Distro { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = "~";

    [JsonPropertyName("shell")]
    public string Shell { get; set; } = "bash";

    [JsonPropertyName("vnc")]
    public EnvironmentVncProfile Vnc { get; set; } = new();

    [JsonPropertyName("commandTimeoutMs")]
    public int CommandTimeoutMs { get; set; } = 30000;
}

public sealed class EnvironmentVncProfile
{
    [JsonPropertyName("display")]
    public string Display { get; set; } = ":10";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 5910;

    [JsonPropertyName("geometry")]
    public string Geometry { get; set; } = "320x170";

    [JsonPropertyName("depth")]
    public int Depth { get; set; } = 24;

    [JsonPropertyName("startupCommand")]
    public string StartupCommand { get; set; } = "openbox & xterm";
}

public sealed class EnvironmentProfilesDocument
{
    [JsonPropertyName("profiles")]
    public List<EnvironmentProfile> Profiles { get; set; } = [];
}

public sealed class EnvironmentProfilesResult
{
    [JsonPropertyName("profiles")]
    public List<EnvironmentProfileSummary> Profiles { get; set; } = [];
}

public sealed class EnvironmentProfileSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("distro")]
    public string Distro { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("vncDisplay")]
    public string VncDisplay { get; set; } = string.Empty;

    [JsonPropertyName("vncPort")]
    public int VncPort { get; set; }

    [JsonPropertyName("vncGeometry")]
    public string VncGeometry { get; set; } = string.Empty;
}

public sealed class EnvironmentStateResult
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("vnc")]
    public EnvironmentVncState Vnc { get; set; } = new();
}

public sealed class EnvironmentVncState
{
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("geometry")]
    public string Geometry { get; set; } = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }
}

public sealed class EnvironmentCommandParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }

    [JsonPropertyName("stdin")]
    public string Stdin { get; set; } = string.Empty;
}

public sealed class EnvironmentCommandResult
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; set; }

    [JsonPropertyName("stdout")]
    public string Stdout { get; set; } = string.Empty;

    [JsonPropertyName("stderr")]
    public string Stderr { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("timedOut")]
    public bool TimedOut { get; set; }
}

public sealed class EnvironmentVncParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    [JsonPropertyName("geometry")]
    public string Geometry { get; set; } = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }
}

public sealed class EnvironmentProcessQueryParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("filter")]
    public string Filter { get; set; } = string.Empty;
}

public sealed class EnvironmentProcessesResult
{
    [JsonPropertyName("processes")]
    public List<EnvironmentProcessInfo> Processes { get; set; } = [];
}

public sealed class EnvironmentProcessInfo
{
    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public string Args { get; set; } = string.Empty;
}

public sealed class EnvironmentKillProcessParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("match")]
    public string Match { get; set; } = string.Empty;

    [JsonPropertyName("signal")]
    public string Signal { get; set; } = "TERM";

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }
}

public sealed class EnvironmentFileParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf-8";

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }
}

public sealed class EnvironmentInstallPackagesParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<string> Packages { get; set; } = [];

    [JsonPropertyName("update")]
    public bool Update { get; set; }

    [JsonPropertyName("sudo")]
    public bool Sudo { get; set; } = true;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 120000;
}

public sealed class EnvironmentLaunchParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("logPath")]
    public string LogPath { get; set; } = string.Empty;

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = [];

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 10000;
}

public sealed class EnvironmentLaunchResult
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("logPath")]
    public string LogPath { get; set; } = string.Empty;
}

public sealed class EnvironmentAppParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("processMatch")]
    public string ProcessMatch { get; set; } = string.Empty;

    [JsonPropertyName("binaryPath")]
    public string BinaryPath { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("logPath")]
    public string LogPath { get; set; } = string.Empty;

    [JsonPropertyName("clearPaths")]
    public List<string> ClearPaths { get; set; } = [];

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = [];

    [JsonPropertyName("killBeforeLaunch")]
    public bool KillBeforeLaunch { get; set; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 10000;
}

public sealed class EnvironmentAppStatusResult
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("cwd")]
    public string Cwd { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("binaryPath")]
    public string BinaryPath { get; set; } = string.Empty;

    [JsonPropertyName("binaryMtime")]
    public string BinaryMtime { get; set; } = string.Empty;

    [JsonPropertyName("logPath")]
    public string LogPath { get; set; } = string.Empty;

    [JsonPropertyName("stateDir")]
    public string StateDir { get; set; } = string.Empty;
}

public sealed class EnvironmentInputDevicesResult
{
    [JsonPropertyName("devices")]
    public List<EnvironmentInputDevice> Devices { get; set; } = [];

    [JsonPropertyName("warning")]
    public string Warning { get; set; } = "This observes target Linux evdev devices. VNC-injected keys may not appear here.";
}

public sealed class EnvironmentInputDevice
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class EnvironmentEvdevCaptureParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("device")]
    public string Device { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; } = 1000;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 5000;
}

public sealed class EnvironmentEvdevCaptureResult
{
    [JsonPropertyName("device")]
    public string Device { get; set; } = string.Empty;

    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    [JsonPropertyName("warning")]
    public string Warning { get; set; } = "This observes target Linux evdev events. VNC-injected keys may not appear here.";
}

public sealed class EnvironmentTailFileParams
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("lines")]
    public int Lines { get; set; } = 80;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }
}

public sealed class EnvironmentFileResult
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public int Bytes { get; set; }
}

public sealed class EnvironmentOperationResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; } = true;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public EnvironmentCommandResult? Command { get; set; }
}
