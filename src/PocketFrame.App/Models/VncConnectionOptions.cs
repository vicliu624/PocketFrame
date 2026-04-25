namespace PocketFrame.App.Models;

public sealed class VncConnectionOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5910;
    public string Password { get; set; } = string.Empty;
}
