using Avalonia.Input;

namespace PocketFrame.App.Services;

public sealed class InputMappingService : IInputMappingService
{
    private static readonly IReadOnlyDictionary<string, uint> NamedKeysyms = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
    {
        ["Backspace"] = 0xff08,
        ["Tab"] = 0xff09,
        ["Enter"] = 0xff0d,
        ["Escape"] = 0xff1b,
        ["Shift"] = 0xffe1,
        ["Ctrl"] = 0xffe3,
        ["Control"] = 0xffe3,
        ["Alt"] = 0xffe9,
        ["Home"] = 0xff50,
        ["Left"] = 0xff51,
        ["Up"] = 0xff52,
        ["Right"] = 0xff53,
        ["Down"] = 0xff54,
        ["PageUp"] = 0xff55,
        ["PageDown"] = 0xff56,
        ["Insert"] = 0xff63,
        ["F1"] = 0xffbe,
        ["F2"] = 0xffbf,
        ["F3"] = 0xffc0,
        ["F4"] = 0xffc1,
        ["F5"] = 0xffc2,
        ["F6"] = 0xffc3,
        ["F7"] = 0xffc4,
        ["F8"] = 0xffc5,
        ["F9"] = 0xffc6,
        ["F10"] = 0xffc7,
        ["F11"] = 0xffc8,
        ["F12"] = 0xffc9,
        ["Delete"] = 0xffff,
        ["Space"] = 0x20
    };

    public uint? ToKeysym(Key key) => key switch
    {
        Key.Up => NamedKeysyms["Up"],
        Key.Down => NamedKeysyms["Down"],
        Key.Left => NamedKeysyms["Left"],
        Key.Right => NamedKeysyms["Right"],
        Key.Enter => NamedKeysyms["Enter"],
        Key.Escape => NamedKeysyms["Escape"],
        Key.Back => NamedKeysyms["Backspace"],
        Key.Tab => NamedKeysyms["Tab"],
        Key.Delete => NamedKeysyms["Delete"],
        Key.Home => NamedKeysyms["Home"],
        Key.LeftShift => NamedKeysyms["Shift"],
        Key.RightShift => NamedKeysyms["Shift"],
        Key.LeftCtrl => NamedKeysyms["Ctrl"],
        Key.RightCtrl => NamedKeysyms["Ctrl"],
        Key.LeftAlt => NamedKeysyms["Alt"],
        Key.RightAlt => NamedKeysyms["Alt"],
        Key.F1 => NamedKeysyms["F1"],
        Key.F2 => NamedKeysyms["F2"],
        Key.Space => NamedKeysyms["Space"],
        >= Key.A and <= Key.Z => (uint)('a' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (uint)('0' + (key - Key.D0)),
        _ => null
    };

    public uint? ToKeysym(string keyCode)
    {
        if (NamedKeysyms.TryGetValue(keyCode, out var keysym))
        {
            return keysym;
        }

        if (keyCode.StartsWith("Char:", StringComparison.Ordinal) && keyCode.Length == 6)
        {
            return keyCode[5];
        }

        return keyCode.Length == 1 ? char.ToLowerInvariant(keyCode[0]) : null;
    }
}
