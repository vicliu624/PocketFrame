using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PocketFrame.App.Automation;
using PocketFrame.App.Services;
using PocketFrame.App.Utils;
using PocketFrame.App.ViewModels;
using PocketFrame.App.Views;

namespace PocketFrame.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly ConnectionProfileService connectionProfileService = new();
    private readonly AutomationPipeServer automationPipeServer;
    private readonly IVncClientService vncClientService;
    private int suppressedTextInputCount;

    public MainWindow()
    {
        InitializeComponent();
        vncClientService = new VncClientService();
        var inputMappingService = new InputMappingService();
        var screenshotService = new ScreenshotService();
        viewModel = new MainWindowViewModel(
            new DeviceProfileService(),
            vncClientService,
            inputMappingService,
            screenshotService,
            new RecordingService(),
            connectionProfileService);
        DataContext = viewModel;
        var automationService = new AutomationService(viewModel, DeviceShell, vncClientService, inputMappingService, screenshotService);
        automationPipeServer = new AutomationPipeServer(automationService);
        Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync();
            automationPipeServer.Start();
            viewModel.AutomationStatus = "Automation: MCP pipe ready";
            viewModel.LogAutomationActivity("MCP pipe ready");
        };
        Closed += async (_, _) => await automationPipeServer.DisposeAsync();
        automationPipeServer.ActivityChanged += (_, activity) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => viewModel.LogAutomationActivity(activity.DisplayText));
        viewModel.FocusSimulatorRequested += (_, _) => DeviceShell.Focus();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        DeviceShell.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        DeviceShell.AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
        DeviceShell.AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private async void OnConnectMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new VncConnectionDialog(connectionProfileService, viewModel.Profiles.ToList(), viewModel.ConnectionProfiles.ToList());
        var profile = await dialog.ShowDialog<Models.ConnectionProfile?>(this);
        viewModel.ConnectionProfiles.Clear();
        foreach (var item in dialog.Profiles)
        {
            viewModel.ConnectionProfiles.Add(item);
        }

        if (profile is not null)
        {
            await viewModel.ConnectAsync(profile);
            DeviceShell.Focus();
        }
    }

    private async void OnDisconnectMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await viewModel.DisconnectAsync();
    }

    private async void OnScreenshotMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await viewModel.CaptureDeviceAsync(DeviceShell);
    }

    private void OnExitMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
    private async void OnAutomationInspectorMenuClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = new AutomationInspectorWindow(viewModel, automationPipeServer, vncClientService);
        await window.ShowDialog(this);
    }

    private void OnScale1Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => viewModel.SetScale(1);
    private void OnScale2Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => viewModel.SetScale(2);
    private void OnScale3Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => viewModel.SetScale(3);
    private void OnScale4Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => viewModel.SetScale(4);

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (ShouldIgnoreHostKey(e.Source))
        {
            InputDiagnostics.Write("Input", $"Ignored KeyDown {e.Key} from {e.Source?.GetType().Name}");
            return;
        }

        if (TryGetPrintableKeysym(e.Key, e.KeyModifiers, out var printableKeysym))
        {
            suppressedTextInputCount++;
            await viewModel.SendKeysymAsync(printableKeysym, $"KeyDown {e.Key} ({FormatKeysym(printableKeysym)})");
            e.Handled = true;
            return;
        }

        await viewModel.SendHostKeyAsync(e.Key, true);
        e.Handled = true;
    }

    private async void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (ShouldIgnoreHostKey(e.Source))
        {
            InputDiagnostics.Write("Input", $"Ignored KeyUp {e.Key} from {e.Source?.GetType().Name}");
            return;
        }

        if (TryGetPrintableKeysym(e.Key, e.KeyModifiers, out _))
        {
            e.Handled = true;
            return;
        }

        await viewModel.SendHostKeyAsync(e.Key, false);
        e.Handled = true;
    }

    private async void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (ShouldIgnoreHostKey(e.Source) || string.IsNullOrEmpty(e.Text))
        {
            InputDiagnostics.Write("Input", $"Ignored TextInput '{e.Text}' from {e.Source?.GetType().Name}");
            return;
        }

        if (suppressedTextInputCount > 0)
        {
            suppressedTextInputCount--;
            viewModel.LogInput($"TextInput suppressed: {e.Text}");
            e.Handled = true;
            return;
        }

        await viewModel.SendTextInputAsync(e.Text);
        e.Handled = true;
    }

    private static bool ShouldIgnoreHostKey(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        return control is TextBox or NumericUpDown or ComboBox ||
               control.FindAncestorOfType<TextBox>() is not null ||
               control.FindAncestorOfType<NumericUpDown>() is not null ||
               control.FindAncestorOfType<ComboBox>() is not null;
    }

    private static bool TryGetPrintableKeysym(Key key, KeyModifiers modifiers, out uint keysym)
    {
        var shifted = modifiers.HasFlag(KeyModifiers.Shift);
        keysym = key switch
        {
            >= Key.A and <= Key.Z => (uint)((shifted ? 'A' : 'a') + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => GetDigitKeysym(key, shifted),
            >= Key.NumPad0 and <= Key.NumPad9 => (uint)('0' + (key - Key.NumPad0)),
            Key.Space => ' ',
            Key.OemPlus => shifted ? '+' : '=',
            Key.OemMinus => shifted ? '_' : '-',
            Key.OemComma => shifted ? '<' : ',',
            Key.OemPeriod => shifted ? '>' : '.',
            Key.OemQuestion => shifted ? '?' : '/',
            Key.Oem1 => shifted ? ':' : ';',
            Key.Oem3 => shifted ? '~' : '`',
            Key.Oem4 => shifted ? '{' : '[',
            Key.Oem5 => shifted ? '|' : '\\',
            Key.Oem6 => shifted ? '}' : ']',
            Key.Oem7 => shifted ? '"' : '\'',
            _ => 0
        };

        return keysym != 0;
    }

    private static uint GetDigitKeysym(Key key, bool shifted)
    {
        if (!shifted)
        {
            return (uint)('0' + (key - Key.D0));
        }

        return key switch
        {
            Key.D0 => ')',
            Key.D1 => '!',
            Key.D2 => '@',
            Key.D3 => '#',
            Key.D4 => '$',
            Key.D5 => '%',
            Key.D6 => '^',
            Key.D7 => '&',
            Key.D8 => '*',
            Key.D9 => '(',
            _ => 0
        };
    }

    private static string FormatKeysym(uint keysym) =>
        keysym is >= 0x20 and <= 0x7e ? $"'{(char)keysym}'/0x{keysym:x}" : $"0x{keysym:x}";
}
