using PocketFrame.Automation;
using PocketFrame.Environments;

namespace PocketFrame.App.Automation;

public interface IAutomationService
{
    Task<AutomationState> GetStateAsync();
    Task<InputModelResult> GetInputModelAsync();
    Task<KeyboardStateResult> GetKeyboardStateAsync();
    Task<DeviceProfilesResult> GetProfilesAsync();
    Task<ConnectionProfilesResult> GetConnectionsAsync();
    Task<AutomationOperationResult> SelectDeviceAsync(string deviceId);
    Task<AutomationOperationResult> SetScaleAsync(double scale);
    Task<AutomationOperationResult> ConnectVncAsync(ConnectVncParams parameters);
    Task<AutomationOperationResult> DisconnectVncAsync();
    Task<EnvironmentProfilesResult> GetEnvironmentProfilesAsync();
    Task<EnvironmentStateResult> GetEnvironmentStateAsync(string profileId);
    Task<EnvironmentCommandResult> ExecuteEnvironmentCommandAsync(EnvironmentCommandParams parameters);
    Task<EnvironmentOperationResult> StartEnvironmentVncAsync(EnvironmentVncParams parameters);
    Task<EnvironmentOperationResult> StopEnvironmentVncAsync(EnvironmentVncParams parameters);
    Task<EnvironmentOperationResult> RestartEnvironmentVncAsync(EnvironmentVncParams parameters);
    Task<EnvironmentProcessesResult> GetEnvironmentProcessesAsync(EnvironmentProcessQueryParams parameters);
    Task<EnvironmentOperationResult> KillEnvironmentProcessAsync(EnvironmentKillProcessParams parameters);
    Task<EnvironmentFileResult> ReadEnvironmentFileAsync(EnvironmentFileParams parameters);
    Task<EnvironmentOperationResult> WriteEnvironmentFileAsync(EnvironmentFileParams parameters);
    Task<EnvironmentOperationResult> InstallEnvironmentPackagesAsync(EnvironmentInstallPackagesParams parameters);
    Task<EnvironmentLaunchResult> LaunchEnvironmentProcessAsync(EnvironmentLaunchParams parameters);
    Task<EnvironmentFileResult> TailEnvironmentFileAsync(EnvironmentTailFileParams parameters);
    Task<FrameHashResult> GetFrameHashAsync();
    Task<CaptureResult> CaptureScreenAsync(string? outputPath);
    Task<CaptureResult> CaptureDeviceAsync(string? outputPath);
    Task TypeTextAsync(string text);
    Task PressKeyAsync(string key);
    Task PressButtonAsync(ButtonPressParams parameters);
    Task ClickScreenAsync(int x, int y, string button);
    Task<WaitResult> WaitAsync(int durationMs);
    Task<WaitFrameResult> WaitFrameChangeAsync(long? afterFrame, int timeoutMs);
    Task<WaitStableFrameResult> WaitStableFrameAsync(int quietMs, int timeoutMs);
}
