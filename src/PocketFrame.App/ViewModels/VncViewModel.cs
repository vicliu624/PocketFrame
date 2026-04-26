using PocketFrame.App.Models;
using PocketFrame.App.Services;
using PocketFrame.App.Utils;
using PocketFrame.App.Vnc;

namespace PocketFrame.App.ViewModels;

public sealed class VncViewModel : ObservableObject
{
    private readonly IVncClientService vncClientService;
    private string host = "127.0.0.1";
    private int port = 5910;
    private string password = string.Empty;
    private string status = "Disconnected";
    private RfbFramebuffer? framebuffer;
    private long frameVersion;

    public VncViewModel(IVncClientService vncClientService)
    {
        this.vncClientService = vncClientService;
    }

    public string Host { get => host; set => SetProperty(ref host, value); }
    public int Port { get => port; set => SetProperty(ref port, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public string Status
    {
        get => status;
        set
        {
            if (SetProperty(ref status, value))
            {
                OnPropertyChanged(nameof(ShowPlaceholder));
            }
        }
    }

    public RfbFramebuffer? Framebuffer
    {
        get => framebuffer;
        set
        {
            if (SetProperty(ref framebuffer, value))
            {
                OnPropertyChanged(nameof(HasFramebuffer));
                OnPropertyChanged(nameof(ShowPlaceholder));
            }
        }
    }

    public long FrameVersion
    {
        get => frameVersion;
        private set => SetProperty(ref frameVersion, value);
    }

    public bool HasFramebuffer => Framebuffer is not null;
    public bool ShowPlaceholder => Framebuffer is null && !Status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase);

    public VncConnectionOptions CreateOptions() => new() { Host = Host, Port = Port, Password = Password };

    public void UpdateFramebuffer(RfbFramebuffer nextFramebuffer)
    {
        var hadFramebuffer = framebuffer is not null;
        framebuffer = nextFramebuffer;
        FrameVersion++;
        OnPropertyChanged(nameof(Framebuffer));
        if (!hadFramebuffer)
        {
            OnPropertyChanged(nameof(HasFramebuffer));
            OnPropertyChanged(nameof(ShowPlaceholder));
        }
    }
}
