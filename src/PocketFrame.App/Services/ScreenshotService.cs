using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PocketFrame.App.Services;

public sealed class ScreenshotService : IScreenshotService
{
    public async Task<string> CaptureDeviceAsync(Control target, string deviceId, string? outputDirectory = null)
    {
        var directory = outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "captures");
        Directory.CreateDirectory(directory);
        var fileName = $"PocketFrame_{deviceId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var path = Path.Combine(directory, fileName);

        var size = target.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new InvalidOperationException("The simulator view is not ready for capture.");
        }

        var pixelSize = new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(target);
        await using var stream = File.Create(path);
        bitmap.Save(stream);
        return path;
    }
}
