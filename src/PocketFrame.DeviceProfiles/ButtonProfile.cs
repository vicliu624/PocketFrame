namespace PocketFrame.DeviceProfiles;

public sealed class ButtonProfile
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string KeyCode { get; set; } = string.Empty;
    public string LongPressKeyCode { get; set; } = string.Empty;
    public string BlueKeyCode { get; set; } = string.Empty;
    public string OrangeKeyCode { get; set; } = string.Empty;
    public string SymKeyCode { get; set; } = string.Empty;
    public string FnKeyCode { get; set; } = string.Empty;
    public string ShiftKeyCode { get; set; } = string.Empty;
    public string Role { get; set; } = "button";
    public string Description { get; set; } = string.Empty;
}
