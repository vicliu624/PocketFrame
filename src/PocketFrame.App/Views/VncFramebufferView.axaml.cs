using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PocketFrame.App.Utils;
using PocketFrame.App.Vnc;
using System.Runtime.InteropServices;

namespace PocketFrame.App.Views;

public partial class VncFramebufferView : UserControl
{
    public static readonly StyledProperty<RfbFramebuffer?> FramebufferProperty =
        AvaloniaProperty.Register<VncFramebufferView, RfbFramebuffer?>(nameof(Framebuffer));

    public VncFramebufferView()
    {
        InitializeComponent();
    }

    public RfbFramebuffer? Framebuffer
    {
        get => GetValue(FramebufferProperty);
        set => SetValue(FramebufferProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FramebufferProperty && change.NewValue is RfbFramebuffer framebuffer)
        {
            UpdateBitmap(framebuffer);
        }
    }

    private void UpdateBitmap(RfbFramebuffer framebuffer)
    {
        InputDiagnostics.Write("VNC", $"Render framebuffer bitmap {framebuffer.Width}x{framebuffer.Height}");
        var bitmap = new WriteableBitmap(
            new PixelSize(framebuffer.Width, framebuffer.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        using (var locked = bitmap.Lock())
        {
            Marshal.Copy(framebuffer.Snapshot(), 0, locked.Address, framebuffer.Width * framebuffer.Height * 4);
        }

        FramebufferImage.Source = bitmap;
    }
}
