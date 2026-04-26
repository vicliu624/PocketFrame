using PocketFrame.App.Services;
using PocketFrame.App.Utils;
using PocketFrame.Automation;
using PocketFrame.DeviceProfiles;

namespace PocketFrame.App.ViewModels;

public sealed class DeviceShellViewModel : ObservableObject
{
    private static readonly HashSet<string> ModifierKeys = new(StringComparer.OrdinalIgnoreCase) { "Shift", "Ctrl", "Control", "Alt", "Fn", "Blue", "Orange", "Sym", "Aa" };
    private static readonly HashSet<string> LayerKeys = new(StringComparer.OrdinalIgnoreCase) { "Blue", "Orange", "Fn", "Sym", "Aa" };
    private static readonly HashSet<string> TransportModifierKeys = new(StringComparer.OrdinalIgnoreCase) { "Shift", "Ctrl", "Control", "Alt" };
    private DeviceProfile? profile;
    private double displayScale = 1;
    private readonly List<string> latchedModifiers = [];
    private readonly List<string> activeLongPressModifiers = [];

    public DeviceShellViewModel(IVncClientService vncClientService, IInputMappingService inputMappingService)
    {
        VncClientService = vncClientService;
        InputMappingService = inputMappingService;
    }

    public IVncClientService VncClientService { get; }
    public IInputMappingService InputMappingService { get; }
    public DeviceProfile? Profile { get => profile; set => SetProperty(ref profile, value); }
    public double DisplayScale { get => displayScale; set => SetProperty(ref displayScale, value); }
    public VncViewModel? Vnc { get; set; }
    public event EventHandler<string>? InputStatusChanged;
    public IReadOnlyList<string> ActiveLayers => latchedModifiers.ToList();

    public async Task<InputActionResult> SendButtonAsync(ButtonProfile button)
    {
        var activeLayersBefore = ActiveLayers.ToList();
        LogInput($"Button click {button.Id}/{button.Label} -> {button.KeyCode}");

        if (ModifierKeys.Contains(button.KeyCode))
        {
            ToggleModifier(button.KeyCode);
            LogInput($"Modifier latched {button.KeyCode}");
            return CreateButtonResult(button, activeLayersBefore, ActiveLayers, button.KeyCode, button.KeyCode, [], "modifier");
        }

        var resolvedKeyCode = ResolveLayeredKeyCode(button);
        if (resolvedKeyCode.Equals("Reboot", StringComparison.OrdinalIgnoreCase))
        {
            await SendRebootChordAsync(button);
            latchedModifiers.Clear();
            var ctrl = InputMappingService.ToKeysym("Ctrl");
            var alt = InputMappingService.ToKeysym("Alt");
            var delete = InputMappingService.ToKeysym("Delete");
            var rebootEvents = new List<InputEmittedEvent>();
            if (ctrl is not null)
            {
                rebootEvents.Add(Emitted("Ctrl", ctrl.Value, "down"));
            }

            if (alt is not null)
            {
                rebootEvents.Add(Emitted("Alt", alt.Value, "down"));
            }

            if (delete is not null)
            {
                rebootEvents.Add(Emitted("Delete", delete.Value, "tap"));
            }

            if (alt is not null)
            {
                rebootEvents.Add(Emitted("Alt", alt.Value, "up"));
            }

            if (ctrl is not null)
            {
                rebootEvents.Add(Emitted("Ctrl", ctrl.Value, "up"));
            }

            return CreateButtonResult(button, activeLayersBefore, ActiveLayers, "Reboot", button.KeyCode, rebootEvents, SelectedLayer(activeLayersBefore));
        }

        var keysym = InputMappingService.ToKeysym(resolvedKeyCode);
        if (keysym is null)
        {
            LogInput($"Button {button.Id} unmapped key {resolvedKeyCode}");
            return CreateButtonResult(button, activeLayersBefore, ActiveLayers, resolvedKeyCode, button.KeyCode, [], SelectedLayer(activeLayersBefore));
        }

        var activeModifiers = latchedModifiers.Where(TransportModifierKeys.Contains).ToArray();
        var emitted = new List<InputEmittedEvent>();
        foreach (var modifier in activeModifiers)
        {
            var modifierKeysym = InputMappingService.ToKeysym(modifier);
            if (modifierKeysym is not null)
            {
                LogInput($"Button modifier down {modifier} -> 0x{modifierKeysym.Value:x}");
                await VncClientService.SendKeyAsync(modifierKeysym.Value, true);
                emitted.Add(Emitted(modifier, modifierKeysym.Value, "down"));
            }
        }

        LogInput($"Button key {button.KeyCode} resolved {resolvedKeyCode} -> 0x{keysym.Value:x} down/up");
        await VncClientService.SendKeyAsync(keysym.Value, true);
        await VncClientService.SendKeyAsync(keysym.Value, false);
        emitted.Add(Emitted(resolvedKeyCode, keysym.Value, "tap"));

        foreach (var modifier in activeModifiers.Reverse())
        {
            var modifierKeysym = InputMappingService.ToKeysym(modifier);
            if (modifierKeysym is not null)
            {
                LogInput($"Button modifier up {modifier} -> 0x{modifierKeysym.Value:x}");
                await VncClientService.SendKeyAsync(modifierKeysym.Value, false);
                emitted.Add(Emitted(modifier, modifierKeysym.Value, "up"));
            }
        }

        latchedModifiers.RemoveAll(modifier => LayerKeys.Contains(modifier) || TransportModifierKeys.Contains(modifier));
        return CreateButtonResult(button, activeLayersBefore, ActiveLayers, resolvedKeyCode, button.KeyCode, emitted, SelectedLayer(activeLayersBefore));
    }

    public async Task BeginButtonPressAsync(ButtonProfile button)
    {
        LogInput($"Button long-press begin {button.Id}/{button.Label} -> {button.KeyCode}");

        if (LayerKeys.Contains(button.KeyCode))
        {
            ToggleModifier(button.KeyCode);
            LogInput($"Layer latched {button.KeyCode}");
            return;
        }

        var resolvedKeyCode = ResolveLayeredKeyCode(button, preferLongPressKey: true);
        if (resolvedKeyCode.Equals("Reboot", StringComparison.OrdinalIgnoreCase))
        {
            await SendRebootChordAsync(button);
            latchedModifiers.Clear();
            return;
        }

        var keysym = InputMappingService.ToKeysym(resolvedKeyCode);
        if (keysym is null)
        {
            LogInput($"Long-press button {button.Id} unmapped key {resolvedKeyCode}");
            return;
        }

        activeLongPressModifiers.Clear();
        foreach (var modifier in latchedModifiers.Where(TransportModifierKeys.Contains))
        {
            var modifierKeysym = InputMappingService.ToKeysym(modifier);
            if (modifierKeysym is not null)
            {
                LogInput($"Long-press modifier down {modifier} -> 0x{modifierKeysym.Value:x}");
                await VncClientService.SendKeyAsync(modifierKeysym.Value, true);
                activeLongPressModifiers.Add(modifier);
            }
        }

        LogInput($"Long-press key down {button.KeyCode} resolved {resolvedKeyCode} -> 0x{keysym.Value:x}");
        await VncClientService.SendKeyAsync(keysym.Value, true);
    }

    public async Task EndButtonPressAsync(ButtonProfile button)
    {
        LogInput($"Button long-press end {button.Id}/{button.Label} -> {button.KeyCode}");

        if (LayerKeys.Contains(button.KeyCode))
        {
            ToggleModifier(button.KeyCode);
            LogInput($"Layer unlatched {button.KeyCode}");
            return;
        }

        var resolvedKeyCode = ResolveLayeredKeyCode(button, preferLongPressKey: true);
        if (resolvedKeyCode.Equals("Reboot", StringComparison.OrdinalIgnoreCase))
        {
            await SendRebootChordAsync(button);
            return;
        }

        var keysym = InputMappingService.ToKeysym(resolvedKeyCode);
        if (keysym is not null)
        {
            LogInput($"Long-press key up {button.KeyCode} resolved {resolvedKeyCode} -> 0x{keysym.Value:x}");
            await VncClientService.SendKeyAsync(keysym.Value, false);
        }

        foreach (var modifier in activeLongPressModifiers.AsEnumerable().Reverse())
        {
            var modifierKeysym = InputMappingService.ToKeysym(modifier);
            if (modifierKeysym is not null)
            {
                LogInput($"Long-press modifier up {modifier} -> 0x{modifierKeysym.Value:x}");
                await VncClientService.SendKeyAsync(modifierKeysym.Value, false);
            }
        }

        activeLongPressModifiers.Clear();
        latchedModifiers.Clear();
    }

    private async Task SendRebootChordAsync(ButtonProfile button)
    {
        LogInput($"Button {button.Id}/{button.Label} -> reboot chord Ctrl+Alt+Delete");
        var ctrl = InputMappingService.ToKeysym("Ctrl");
        var alt = InputMappingService.ToKeysym("Alt");
        var delete = InputMappingService.ToKeysym("Delete");
        if (ctrl is null || alt is null || delete is null)
        {
            LogInput("Reboot chord unmapped");
            return;
        }

        await VncClientService.SendKeyAsync(ctrl.Value, true);
        await VncClientService.SendKeyAsync(alt.Value, true);
        await VncClientService.SendKeyAsync(delete.Value, true);
        await VncClientService.SendKeyAsync(delete.Value, false);
        await VncClientService.SendKeyAsync(alt.Value, false);
        await VncClientService.SendKeyAsync(ctrl.Value, false);
    }

    private void ToggleModifier(string keyCode)
    {
        var existing = latchedModifiers.FindIndex(modifier => modifier.Equals(keyCode, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            latchedModifiers.RemoveAt(existing);
            return;
        }

        latchedModifiers.Add(keyCode);
    }

    private string ResolveLayeredKeyCode(ButtonProfile button, bool preferLongPressKey = false)
    {
        if (preferLongPressKey && !string.IsNullOrWhiteSpace(button.LongPressKeyCode))
        {
            return button.LongPressKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Orange", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Fn", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(button.FnKeyCode))
        {
            return button.FnKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Orange", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Fn", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(button.OrangeKeyCode))
        {
            return button.OrangeKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Blue", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Sym", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(button.SymKeyCode))
        {
            return button.SymKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Blue", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Sym", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(button.BlueKeyCode))
        {
            return button.BlueKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Aa", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Shift", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(button.ShiftKeyCode))
        {
            return button.ShiftKeyCode;
        }

        if (latchedModifiers.Any(modifier => modifier.Equals("Aa", StringComparison.OrdinalIgnoreCase) || modifier.Equals("Shift", StringComparison.OrdinalIgnoreCase)) &&
            button.KeyCode.Length == 1 &&
            char.IsLetter(button.KeyCode[0]))
        {
            return $"Char:{char.ToUpperInvariant(button.KeyCode[0])}";
        }

        return button.KeyCode;
    }

    private static InputActionResult CreateButtonResult(
        ButtonProfile button,
        IReadOnlyList<string> activeLayersBefore,
        IReadOnlyList<string> activeLayersAfter,
        string resolvedKey,
        string sourceKey,
        List<InputEmittedEvent> emitted,
        string selectedLayer)
    {
        var primary = emitted.FirstOrDefault(item =>
            item.Phase.Equals("tap", StringComparison.OrdinalIgnoreCase) ||
            item.Phase.StartsWith("hold:", StringComparison.OrdinalIgnoreCase)) ?? emitted.FirstOrDefault();
        return new InputActionResult
        {
            RequestedAction = "press_button",
            RequestedButtonId = button.Id,
            InputLayer = "pocketframe-profile",
            ActiveLayersBefore = activeLayersBefore.ToList(),
            ActiveLayersAfter = activeLayersAfter.ToList(),
            ResolvedKey = resolvedKey,
            EmittedTransport = primary?.Transport ?? "vnc",
            EmittedKey = primary?.Key ?? resolvedKey,
            EmittedKeysym = primary?.Keysym ?? string.Empty,
            Resolved = new InputResolvedButton
            {
                ProfileButtonId = button.Id,
                Label = button.Label,
                SelectedLayer = selectedLayer,
                ResolvedKey = resolvedKey,
                SourceKey = sourceKey
            },
            Emitted = emitted,
            Warnings =
            {
                "press_button uses PocketFrame device profile projection.",
                "VNC key events are not Linux evdev events."
            }
        };
    }

    private static string SelectedLayer(IReadOnlyList<string> activeLayers) =>
        activeLayers.FirstOrDefault(layer => LayerKeys.Contains(layer)) ?? "normal";

    private static InputEmittedEvent Emitted(string key, uint keysym, string phase) => new()
    {
        Transport = "vnc",
        Key = key,
        Keysym = $"0x{keysym:x}",
        Phase = phase
    };

    private void LogInput(string message)
    {
        InputDiagnostics.Write("Input", message);
        InputStatusChanged?.Invoke(this, message);
    }
}
