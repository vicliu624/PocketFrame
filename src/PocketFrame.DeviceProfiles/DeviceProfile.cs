using System.Collections.ObjectModel;

namespace PocketFrame.DeviceProfiles;

public sealed class DeviceProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public double ShellWidth { get; set; }
    public double ShellHeight { get; set; }
    public double ScreenX { get; set; }
    public double ScreenY { get; set; }
    public double ScreenScale { get; set; } = 1;
    public string BackgroundColor { get; set; } = "#10131a";
    public string ShellAssetPath { get; set; } = string.Empty;
    public ObservableCollection<ButtonProfile> Buttons { get; set; } = [];
}
