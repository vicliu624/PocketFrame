using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace PocketFrame.App.Vnc;

public static class RfbProtocol
{
    public static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("RFB connection closed by remote host.");
            }

            offset += read;
        }

        return buffer;
    }

    public static async Task WriteAsync(NetworkStream stream, byte[] data, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static ushort ReadUInt16(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt16BigEndian(data);
    public static uint ReadUInt32(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt32BigEndian(data);
    public static void WriteUInt16(Span<byte> data, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(data, value);
    public static void WriteUInt32(Span<byte> data, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data, value);

    public static byte[] CreateVncAuthResponse(byte[] challenge, string password)
    {
        var key = new byte[8];
        var passwordBytes = Encoding.ASCII.GetBytes(password ?? string.Empty);
        Array.Copy(passwordBytes, key, Math.Min(passwordBytes.Length, key.Length));
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = ReverseBits(key[index]);
        }

        using var des = DES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        using var encryptor = des.CreateEncryptor(key, null);
        return encryptor.TransformFinalBlock(challenge, 0, challenge.Length);
    }

    private static byte ReverseBits(byte value)
    {
        var result = 0;
        for (var bit = 0; bit < 8; bit++)
        {
            result = (result << 1) | ((value >> bit) & 1);
        }

        return (byte)result;
    }
}
