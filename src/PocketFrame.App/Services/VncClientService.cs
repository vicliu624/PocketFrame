using PocketFrame.App.Models;
using PocketFrame.App.Utils;
using PocketFrame.App.Vnc;

namespace PocketFrame.App.Services;

public sealed class VncClientService : IVncClientService
{
    private readonly RfbClient client = new();
    private bool isDisconnecting;

    public VncClientService()
    {
        client.FramebufferUpdated += (_, framebuffer) =>
        {
            FrameIndex++;
            LastFrameUpdatedAt = DateTimeOffset.Now;
            FramebufferUpdated?.Invoke(this, framebuffer);
        };
        client.StatusChanged += (_, status) => StatusChanged?.Invoke(this, status);
    }

    public event EventHandler<RfbFramebuffer>? FramebufferUpdated;
    public event EventHandler<string>? StatusChanged;
    public RfbFramebuffer? Framebuffer => client.Framebuffer;
    public long FrameIndex { get; private set; }
    public DateTimeOffset? LastFrameUpdatedAt { get; private set; }
    public Task ConnectAsync(VncConnectionOptions options, CancellationToken cancellationToken = default) => client.ConnectAsync(options.Host, options.Port, options.Password, cancellationToken);
    public Task DisconnectAsync() => client.DisconnectAsync();
    public Task SendKeyAsync(uint keysym, bool isDown)
    {
        InputDiagnostics.Write("VNC", $"SendKey {(isDown ? "down" : "up")} 0x{keysym:x}");
        return SendInputSafelyAsync(() => client.InputSender.SendKeyAsync(keysym, isDown));
    }

    public Task SendPointerAsync(int x, int y, byte buttonMask)
    {
        InputDiagnostics.Write("VNC", $"SendPointer x={x} y={y} mask={buttonMask}");
        return SendInputSafelyAsync(() => client.InputSender.SendPointerAsync(x, y, buttonMask));
    }

    private async Task SendInputSafelyAsync(Func<Task> send)
    {
        await SendSafelyAsync(send);
        await SendSafelyAsync(() => client.RequestRefreshAsync());
    }

    private async Task SendSafelyAsync(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex) when (IsDisconnectedWrite(ex))
        {
            StatusChanged?.Invoke(this, $"Disconnected: {ex.Message}");
            if (!isDisconnecting)
            {
                isDisconnecting = true;
                try
                {
                    await client.DisconnectAsync();
                }
                finally
                {
                    isDisconnecting = false;
                }
            }
        }
    }

    private static bool IsDisconnectedWrite(Exception exception) =>
        exception is IOException or ObjectDisposedException or InvalidOperationException ||
        exception.InnerException is IOException or ObjectDisposedException or System.Net.Sockets.SocketException;
}
