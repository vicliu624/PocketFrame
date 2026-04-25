using System.Net.Sockets;

namespace PocketFrame.App.Vnc;

public sealed class RfbInputSender
{
    private readonly Func<NetworkStream?> streamAccessor;
    private readonly Func<byte[], CancellationToken, Task> writer;

    public RfbInputSender(Func<NetworkStream?> streamAccessor, Func<byte[], CancellationToken, Task> writer)
    {
        this.streamAccessor = streamAccessor;
        this.writer = writer;
    }

    public async Task SendKeyAsync(uint keysym, bool isDown, CancellationToken cancellationToken = default)
    {
        var stream = streamAccessor();
        if (stream is null || !stream.CanWrite)
        {
            return;
        }

        var message = new byte[8];
        message[0] = 4;
        message[1] = isDown ? (byte)1 : (byte)0;
        RfbProtocol.WriteUInt32(message.AsSpan(4), keysym);
        await writer(message, cancellationToken);
    }

    public async Task SendPointerAsync(int x, int y, byte buttonMask, CancellationToken cancellationToken = default)
    {
        var stream = streamAccessor();
        if (stream is null || !stream.CanWrite)
        {
            return;
        }

        var message = new byte[6];
        message[0] = 5;
        message[1] = buttonMask;
        RfbProtocol.WriteUInt16(message.AsSpan(2), (ushort)Math.Clamp(x, 0, ushort.MaxValue));
        RfbProtocol.WriteUInt16(message.AsSpan(4), (ushort)Math.Clamp(y, 0, ushort.MaxValue));
        await writer(message, cancellationToken);
    }
}
