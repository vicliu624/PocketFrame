using PocketFrame.App.Models;
using PocketFrame.App.Vnc;

namespace PocketFrame.App.Services;

public interface IVncClientService
{
    event EventHandler<RfbFramebuffer>? FramebufferUpdated;
    event EventHandler<string>? StatusChanged;
    RfbFramebuffer? Framebuffer { get; }
    long FrameIndex { get; }
    DateTimeOffset? LastFrameUpdatedAt { get; }
    Task ConnectAsync(VncConnectionOptions options, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendKeyAsync(uint keysym, bool isDown);
    Task SendPointerAsync(int x, int y, byte buttonMask);
}
