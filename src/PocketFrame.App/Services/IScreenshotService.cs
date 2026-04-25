using Avalonia.Controls;

namespace PocketFrame.App.Services;

public interface IScreenshotService
{
    Task<string> CaptureDeviceAsync(Control target, string deviceId, string? outputDirectory = null);
}
