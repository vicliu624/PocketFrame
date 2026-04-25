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

    public VncViewModel(IVncClientService vncClientService)
    {
        this.vncClientService = vncClientService;
    }

    public string Host { get => host; set => SetProperty(ref host, value); }
    public int Port { get => port; set => SetProperty(ref port, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public string Status { get => status; set => SetProperty(ref status, value); }
    public RfbFramebuffer? Framebuffer { get => framebuffer; set => SetProperty(ref framebuffer, value); }

    public VncConnectionOptions CreateOptions() => new() { Host = Host, Port = Port, Password = Password };
}
