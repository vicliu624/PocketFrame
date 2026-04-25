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
