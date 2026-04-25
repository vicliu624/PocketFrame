using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using PocketFrame.App.Utils;

namespace PocketFrame.App.Vnc;

public sealed class RfbClient : IAsyncDisposable
{
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private CancellationTokenSource? receiveCts;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private long framebufferUpdateCount;

    public event EventHandler<RfbFramebuffer>? FramebufferUpdated;
    public event EventHandler<string>? StatusChanged;

    public RfbFramebuffer? Framebuffer { get; private set; }
    public RfbInputSender InputSender { get; }

    public RfbClient()
    {
        InputSender = new RfbInputSender(() => stream, WriteToServerAsync);
    }

    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken)
    {
        try
        {
            await DisconnectAsync();
            StatusChanged?.Invoke(this, $"Connecting {host}:{port}");
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            stream = tcpClient.GetStream();

            var serverVersion = Encoding.ASCII.GetString(await RfbProtocol.ReadExactAsync(stream, 12, cancellationToken));
            var clientVersion = serverVersion.StartsWith("RFB 003.003", StringComparison.Ordinal) ? "RFB 003.003\n" : "RFB 003.008\n";
            await WriteToServerAsync(Encoding.ASCII.GetBytes(clientVersion), cancellationToken);

            if (clientVersion.Contains("003.003", StringComparison.Ordinal))
            {
                await NegotiateSecurity33Async(password, cancellationToken);
            }
            else
            {
                await NegotiateSecurity38Async(password, cancellationToken);
            }

            await WriteToServerAsync([1], cancellationToken);
            var init = await RfbProtocol.ReadExactAsync(stream, 24, cancellationToken);
            var width = RfbProtocol.ReadUInt16(init.AsSpan(0, 2));
            var height = RfbProtocol.ReadUInt16(init.AsSpan(2, 2));
            var nameLength = RfbProtocol.ReadUInt32(init.AsSpan(20, 4));
            if (nameLength > 0)
            {
                _ = await RfbProtocol.ReadExactAsync(stream, (int)nameLength, cancellationToken);
            }

            Framebuffer = new RfbFramebuffer(width, height);
            await SetPixelFormatAsync(cancellationToken);
            await SetEncodingsAsync(cancellationToken);
            await RequestFramebufferUpdateAsync(false, cancellationToken);

            receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => ReceiveLoopAsync(receiveCts.Token), receiveCts.Token);
            StatusChanged?.Invoke(this, $"Connected {width}x{height}");
        }
        catch
        {
            await DisconnectAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        receiveCts?.Cancel();
        receiveCts?.Dispose();
        receiveCts = null;
        stream?.Dispose();
        stream = null;
        tcpClient?.Dispose();
        tcpClient = null;
        await Task.CompletedTask;
        StatusChanged?.Invoke(this, "Disconnected");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    public Task RequestRefreshAsync(CancellationToken cancellationToken = default) =>
        Framebuffer is null ? Task.CompletedTask : RequestFramebufferUpdateAsync(true, cancellationToken);

    private async Task NegotiateSecurity38Async(string password, CancellationToken cancellationToken)
    {
        var count = (await RfbProtocol.ReadExactAsync(stream!, 1, cancellationToken))[0];
        if (count == 0)
        {
            var length = RfbProtocol.ReadUInt32(await RfbProtocol.ReadExactAsync(stream!, 4, cancellationToken));
            var reason = Encoding.ASCII.GetString(await RfbProtocol.ReadExactAsync(stream!, (int)length, cancellationToken));
            throw new InvalidOperationException(reason);
        }

        var types = await RfbProtocol.ReadExactAsync(stream!, count, cancellationToken);
        var selected = types.Contains((byte)2) && !string.IsNullOrEmpty(password) ? (byte)2 : types.Contains((byte)1) ? (byte)1 : (byte)0;
        if (selected == 0)
        {
            throw new NotSupportedException("The VNC server does not offer None or VNC password authentication.");
        }

            await WriteToServerAsync([selected], cancellationToken);
        if (selected == 2)
        {
            await AuthenticateVncAsync(password, cancellationToken);
        }

        var status = RfbProtocol.ReadUInt32(await RfbProtocol.ReadExactAsync(stream!, 4, cancellationToken));
        if (status != 0)
        {
            throw new UnauthorizedAccessException("VNC authentication failed.");
        }
    }

    private async Task NegotiateSecurity33Async(string password, CancellationToken cancellationToken)
    {
        var securityType = RfbProtocol.ReadUInt32(await RfbProtocol.ReadExactAsync(stream!, 4, cancellationToken));
        if (securityType == 0)
        {
            throw new InvalidOperationException("The VNC server refused security negotiation.");
        }

        if (securityType == 2)
        {
            await AuthenticateVncAsync(password, cancellationToken);
            var status = RfbProtocol.ReadUInt32(await RfbProtocol.ReadExactAsync(stream!, 4, cancellationToken));
            if (status != 0)
            {
                throw new UnauthorizedAccessException("VNC authentication failed.");
            }
        }
        else if (securityType != 1)
        {
            throw new NotSupportedException($"Unsupported RFB 3.3 security type {securityType}.");
        }
    }

    private async Task AuthenticateVncAsync(string password, CancellationToken cancellationToken)
    {
        var challenge = await RfbProtocol.ReadExactAsync(stream!, 16, cancellationToken);
        var response = RfbProtocol.CreateVncAuthResponse(challenge, password);
        await WriteToServerAsync(response, cancellationToken);
    }

    private async Task SetPixelFormatAsync(CancellationToken cancellationToken)
    {
        var message = new byte[20];
        message[0] = 0;
        message[4] = 32;
        message[5] = 24;
        message[6] = 0;
        message[7] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(8), 255);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(10), 255);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(12), 255);
        message[14] = 16;
        message[15] = 8;
        message[16] = 0;
        await WriteToServerAsync(message, cancellationToken);
    }

    private async Task SetEncodingsAsync(CancellationToken cancellationToken)
    {
        var message = new byte[8];
        message[0] = 2;
        RfbProtocol.WriteUInt16(message.AsSpan(2), 1);
        RfbProtocol.WriteUInt32(message.AsSpan(4), RfbEncoding.Raw);
        await WriteToServerAsync(message, cancellationToken);
    }

    private async Task RequestFramebufferUpdateAsync(bool incremental, CancellationToken cancellationToken)
    {
        var message = new byte[10];
        message[0] = 3;
        message[1] = incremental ? (byte)1 : (byte)0;
        RfbProtocol.WriteUInt16(message.AsSpan(6), (ushort)(Framebuffer?.Width ?? 0));
        RfbProtocol.WriteUInt16(message.AsSpan(8), (ushort)(Framebuffer?.Height ?? 0));
        await WriteToServerAsync(message, cancellationToken);
    }

    private async Task WriteToServerAsync(byte[] message, CancellationToken cancellationToken)
    {
        var currentStream = stream;
        if (currentStream is null || !currentStream.CanWrite)
        {
            return;
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await RfbProtocol.WriteAsync(currentStream, message, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && stream is not null && Framebuffer is not null)
            {
                var type = (await RfbProtocol.ReadExactAsync(stream, 1, cancellationToken))[0];
                if (type == 0)
                {
                    await ReadFramebufferUpdateAsync(cancellationToken);
                    await RequestFramebufferUpdateAsync(true, cancellationToken);
                }
                else if (type == 2)
                {
                    _ = await RfbProtocol.ReadExactAsync(stream, 1, cancellationToken);
                }
                else if (type == 3)
                {
                    var paddingAndLength = await RfbProtocol.ReadExactAsync(stream, 7, cancellationToken);
                    var length = RfbProtocol.ReadUInt32(paddingAndLength.AsSpan(3, 4));
                    _ = await RfbProtocol.ReadExactAsync(stream, (int)length, cancellationToken);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported RFB server message {type}.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Disconnected: {ex.Message}");
        }
    }

    private async Task ReadFramebufferUpdateAsync(CancellationToken cancellationToken)
    {
        _ = await RfbProtocol.ReadExactAsync(stream!, 1, cancellationToken);
        var count = RfbProtocol.ReadUInt16(await RfbProtocol.ReadExactAsync(stream!, 2, cancellationToken));
        for (var index = 0; index < count; index++)
        {
            var header = await RfbProtocol.ReadExactAsync(stream!, 12, cancellationToken);
            var x = RfbProtocol.ReadUInt16(header.AsSpan(0, 2));
            var y = RfbProtocol.ReadUInt16(header.AsSpan(2, 2));
            var width = RfbProtocol.ReadUInt16(header.AsSpan(4, 2));
            var height = RfbProtocol.ReadUInt16(header.AsSpan(6, 2));
            var encoding = (int)RfbProtocol.ReadUInt32(header.AsSpan(8, 4));
            if (encoding != RfbEncoding.Raw)
            {
                throw new NotSupportedException($"Unsupported RFB encoding {encoding}.");
            }

            var pixels = await RfbProtocol.ReadExactAsync(stream!, width * height * 4, cancellationToken);
            Framebuffer!.UpdateRawRectangle(x, y, width, height, pixels);
        }

        var updateNumber = Interlocked.Increment(ref framebufferUpdateCount);
        InputDiagnostics.Write("VNC", $"Framebuffer update #{updateNumber}: {count} rect(s)");
        FramebufferUpdated?.Invoke(this, Framebuffer!.CloneSnapshot());
    }
}
