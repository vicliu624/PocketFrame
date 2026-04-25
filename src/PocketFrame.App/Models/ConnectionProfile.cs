namespace PocketFrame.App.Models;

public sealed class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Local Cardputer";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5910;
    public string Password { get; set; } = string.Empty;
    public string DeviceId { get; set; } = "cardputer-zero";
    public double Scale { get; set; } = 1;

    public override string ToString() => Name;
}
