using PocketFrame.Automation;

namespace PocketFrame.App.Automation;

public interface IAutomationService
{
    Task<AutomationState> GetStateAsync();
    Task<CaptureResult> CaptureScreenAsync(string? outputPath);
    Task<CaptureResult> CaptureDeviceAsync(string? outputPath);
    Task TypeTextAsync(string text);
    Task PressKeyAsync(string key);
    Task PressButtonAsync(string buttonId);
    Task ClickScreenAsync(int x, int y, string button);
    Task<WaitFrameResult> WaitFrameChangeAsync(long? afterFrame, int timeoutMs);
}
