using PocketFrame.App.Vnc;

namespace PocketFrame.App.Tests;

public sealed class RfbFramebufferTests
{
    [Fact]
    public void FillRectangleWritesExpectedPixels()
    {
        var framebuffer = new RfbFramebuffer(4, 4);
        framebuffer.FillRectangle(1, 1, 2, 2, [1, 2, 3, 4]);

        var snapshot = framebuffer.Snapshot();
        Assert.Equal([1, 2, 3, 4], snapshot.Skip(((1 * 4) + 1) * 4).Take(4).ToArray());
        Assert.Equal([1, 2, 3, 4], snapshot.Skip(((2 * 4) + 2) * 4).Take(4).ToArray());
    }

    [Fact]
    public void CopyRectangleCopiesPixelsThroughTemporaryBuffer()
    {
        var framebuffer = new RfbFramebuffer(4, 4);
        framebuffer.FillRectangle(0, 0, 2, 2, [9, 8, 7, 6]);

        framebuffer.CopyRectangle(0, 0, 2, 2, 2, 2);

        var snapshot = framebuffer.Snapshot();
        Assert.Equal([9, 8, 7, 6], snapshot.Skip(((2 * 4) + 2) * 4).Take(4).ToArray());
        Assert.Equal([9, 8, 7, 6], snapshot.Skip(((3 * 4) + 3) * 4).Take(4).ToArray());
    }
}
