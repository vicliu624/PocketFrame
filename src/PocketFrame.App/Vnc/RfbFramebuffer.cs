namespace PocketFrame.App.Vnc;

public sealed class RfbFramebuffer
{
    private readonly object syncRoot = new();

    public RfbFramebuffer(int width, int height)
    {
        Width = width;
        Height = height;
        BgraPixels = new byte[width * height * 4];
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] BgraPixels { get; }

    public void UpdateRawRectangle(int x, int y, int width, int height, byte[] sourceBgra)
    {
        lock (syncRoot)
        {
            for (var row = 0; row < height; row++)
            {
                var sourceOffset = row * width * 4;
                var targetOffset = ((y + row) * Width + x) * 4;
                Buffer.BlockCopy(sourceBgra, sourceOffset, BgraPixels, targetOffset, width * 4);
            }
        }
    }

    public void CopyRectangle(int sourceX, int sourceY, int targetX, int targetY, int width, int height)
    {
        lock (syncRoot)
        {
            var copy = new byte[width * height * 4];
            for (var row = 0; row < height; row++)
            {
                var sourceOffset = ((sourceY + row) * Width + sourceX) * 4;
                Buffer.BlockCopy(BgraPixels, sourceOffset, copy, row * width * 4, width * 4);
            }

            for (var row = 0; row < height; row++)
            {
                var targetOffset = ((targetY + row) * Width + targetX) * 4;
                Buffer.BlockCopy(copy, row * width * 4, BgraPixels, targetOffset, width * 4);
            }
        }
    }

    public void FillRectangle(int x, int y, int width, int height, ReadOnlySpan<byte> bgra)
    {
        lock (syncRoot)
        {
            for (var row = 0; row < height; row++)
            {
                var offset = ((y + row) * Width + x) * 4;
                for (var column = 0; column < width; column++)
                {
                    bgra.CopyTo(BgraPixels.AsSpan(offset + column * 4, 4));
                }
            }
        }
    }

    public byte[] Snapshot()
    {
        lock (syncRoot)
        {
            return BgraPixels.ToArray();
        }
    }

    public RfbFramebuffer CloneSnapshot()
    {
        lock (syncRoot)
        {
            var clone = new RfbFramebuffer(Width, Height);
            Buffer.BlockCopy(BgraPixels, 0, clone.BgraPixels, 0, BgraPixels.Length);
            return clone;
        }
    }
}
