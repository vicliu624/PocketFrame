namespace PocketFrame.App.Models;

public sealed class ButtonProfile
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string KeyCode { get; set; } = string.Empty;
    public string BlueKeyCode { get; set; } = string.Empty;
    public string OrangeKeyCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
