using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PocketFrame.App.Vnc;

namespace PocketFrame.App.Views;

public partial class VncFramebufferView : UserControl
{
    public static readonly StyledProperty<RfbFramebuffer?> FramebufferProperty =
        AvaloniaProperty.Register<VncFramebufferView, RfbFramebuffer?>(nameof(Framebuffer));

    public static readonly StyledProperty<long> FrameVersionProperty =
        AvaloniaProperty.Register<VncFramebufferView, long>(nameof(FrameVersion));

    private WriteableBitmap? bitmap;

    public VncFramebufferView()
    {
        InitializeComponent();
    }

    public RfbFramebuffer? Framebuffer
    {
        get => GetValue(FramebufferProperty);
        set => SetValue(FramebufferProperty, value);
    }

    public long FrameVersion
    {
        get => GetValue(FrameVersionProperty);
        set => SetValue(FrameVersionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FramebufferProperty || change.Property == FrameVersionProperty)
        {
            UpdateBitmap(Framebuffer);
        }
    }

    private void UpdateBitmap(RfbFramebuffer? framebuffer)
    {
        if (framebuffer is null)
        {
            FramebufferImage.Source = null;
            bitmap = null;
            return;
        }

        var pixelSize = new PixelSize(framebuffer.Width, framebuffer.Height);
        if (bitmap is null || bitmap.PixelSize != pixelSize)
        {
            bitmap = new WriteableBitmap(
                pixelSize,
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            FramebufferImage.Source = bitmap;
        }

        using (var locked = bitmap.Lock())
        {
            framebuffer.CopyTo(locked.Address, locked.RowBytes);
        }

        FramebufferImage.InvalidateVisual();
    }
}
