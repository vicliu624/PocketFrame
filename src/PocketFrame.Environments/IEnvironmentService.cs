namespace PocketFrame.Environments;

public interface IEnvironmentService
{
    Task<EnvironmentProfilesResult> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<EnvironmentStateResult> GetStateAsync(string profileId = "", CancellationToken cancellationToken = default);
    Task<EnvironmentCommandResult> ExecuteAsync(EnvironmentCommandParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> StartVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> StopVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> RestartVncAsync(EnvironmentVncParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentProcessesResult> GetProcessesAsync(EnvironmentProcessQueryParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> KillProcessAsync(EnvironmentKillProcessParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentFileResult> ReadFileAsync(EnvironmentFileParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> WriteFileAsync(EnvironmentFileParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> InstallPackagesAsync(EnvironmentInstallPackagesParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentLaunchResult> LaunchAsync(EnvironmentLaunchParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentFileResult> TailFileAsync(EnvironmentTailFileParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentAppStatusResult> GetAppStatusAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> KillAppAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentLaunchResult> LaunchAppAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentOperationResult> CleanAppStateAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentFileResult> TailAppLogAsync(EnvironmentAppParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentInputDevicesResult> GetInputDevicesAsync(EnvironmentCommandParams parameters, CancellationToken cancellationToken = default);
    Task<EnvironmentEvdevCaptureResult> CaptureEvdevAsync(EnvironmentEvdevCaptureParams parameters, CancellationToken cancellationToken = default);
}
