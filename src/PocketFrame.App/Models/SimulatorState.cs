namespace PocketFrame.App.Models;

public sealed class SimulatorState
{
    public DeviceProfile? ActiveDevice { get; set; }
    public double DisplayScale { get; set; } = 1;
    public string ConnectionStatus { get; set; } = "Disconnected";
    public bool IsConnected { get; set; }
}
