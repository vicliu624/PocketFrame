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
    public KeyboardProfile Keyboard { get; set; } = new();
    public ObservableCollection<ShellAnnotationProfile> ShellAnnotations { get; set; } = [];
}

public sealed class KeyboardProfile
{
    public ObservableCollection<string> Layers { get; set; } = [];
    public ObservableCollection<KeyProfile> Keys { get; set; } = [];
}

public sealed class KeyProfile
{
    public string Id { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Label { get; set; } = string.Empty;
    public string KeyCode { get; set; } = string.Empty;
    public string FnKeyCode { get; set; } = string.Empty;
    public string SymKeyCode { get; set; } = string.Empty;
    public string ShiftKeyCode { get; set; } = string.Empty;
    public string Role { get; set; } = "key";
    public string Fill { get; set; } = string.Empty;
    public ObservableCollection<KeyLegendProfile> Legends { get; set; } = [];
}

public sealed class KeyLegendProfile
{
    public string Text { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public string Color { get; set; } = "#222222";
    public string Position { get; set; } = "topRight";
    public double FontSize { get; set; } = 14;
    public string Weight { get; set; } = "Bold";
}

public sealed class ShellAnnotationProfile
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Kind { get; set; } = "label";
    public string Fill { get; set; } = "#ffffff";
    public string Stroke { get; set; } = string.Empty;
    public string Foreground { get; set; } = "#222222";
    public double FontSize { get; set; } = 14;
    public string Weight { get; set; } = "Bold";
    public double Radius { get; set; } = 0;
}
