using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Net.Sockets;
using PocketFrame.App.Models;
using PocketFrame.App.Services;
using PocketFrame.App.Utils;
using PocketFrame.App.Vnc;

namespace PocketFrame.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDeviceProfileService deviceProfileService;
    private readonly IVncClientService vncClientService;
    private readonly IInputMappingService inputMappingService;
    private readonly IScreenshotService screenshotService;
    private readonly IRecordingService recordingService;
    private readonly IConnectionProfileService connectionProfileService;
    private DeviceProfile? selectedProfile;
    private double selectedScale = 1;
    private string captureStatus = string.Empty;
    private string lastInputStatus = "Input: idle";
    private string automationStatus = "Automation: starting";

    public MainWindowViewModel(
        IDeviceProfileService deviceProfileService,
        IVncClientService vncClientService,
        IInputMappingService inputMappingService,
        IScreenshotService screenshotService,
        IRecordingService recordingService,
        IConnectionProfileService connectionProfileService)
    {
        this.deviceProfileService = deviceProfileService;
        this.vncClientService = vncClientService;
        this.inputMappingService = inputMappingService;
        this.screenshotService = screenshotService;
        this.recordingService = recordingService;
        this.connectionProfileService = connectionProfileService;

        Vnc = new VncViewModel(vncClientService);
        DeviceShell = new DeviceShellViewModel(vncClientService, inputMappingService) { Vnc = Vnc };
        ConnectCommand = new AsyncCommand(ConnectOrDisconnectAsync);
        RecordingCommand = new AsyncCommand(ToggleRecordingAsync);

        vncClientService.FramebufferUpdated += (_, framebuffer) => Dispatcher.UIThread.Post(() => Vnc.Framebuffer = framebuffer);
        vncClientService.StatusChanged += (_, status) => Dispatcher.UIThread.Post(() => Vnc.Status = status);
        DeviceShell.InputStatusChanged += (_, message) => Dispatcher.UIThread.Post(() => LastInputStatus = $"Input: {message}");
    }

    public ObservableCollection<DeviceProfile> Profiles { get; } = new();
    public ObservableCollection<ConnectionProfile> ConnectionProfiles { get; } = new();
    public IReadOnlyList<double> Scales { get; } = PixelScaling.FixedScales;
    public VncViewModel Vnc { get; }
    public DeviceShellViewModel DeviceShell { get; }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand RecordingCommand { get; }

    public DeviceProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (SetProperty(ref selectedProfile, value))
            {
                DeviceShell.Profile = value;
            }
        }
    }

    public double SelectedScale
    {
        get => selectedScale;
        set
        {
            if (SetProperty(ref selectedScale, value))
            {
                DeviceShell.DisplayScale = value;
            }
        }
    }

    public string CaptureStatus { get => captureStatus; set => SetProperty(ref captureStatus, value); }
    public string LastInputStatus { get => lastInputStatus; set => SetProperty(ref lastInputStatus, value); }
    public string AutomationStatus { get => automationStatus; set => SetProperty(ref automationStatus, value); }
    public string RecordingLabel => recordingService.IsRecording ? "Stop Recording" : "Recording Placeholder";
    public event EventHandler? FocusSimulatorRequested;

    public async Task InitializeAsync()
    {
        Profiles.Clear();
        foreach (var profile in await deviceProfileService.LoadProfilesAsync())
        {
            Profiles.Add(profile);
        }

        foreach (var profile in await connectionProfileService.LoadAsync())
        {
            ConnectionProfiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
    }

    public async Task ConnectOrDisconnectAsync()
    {
        if (Vnc.Status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase) || Vnc.Status == "Connecting")
        {
            await vncClientService.DisconnectAsync();
            return;
        }

        try
        {
            await vncClientService.ConnectAsync(Vnc.CreateOptions());
            ApplyFramebufferSizeWarning();
            FocusSimulatorRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (RfbConnectionException ex)
        {
            Vnc.Status = $"Connection failed [{ex.Code}]: {ex.Message}";
        }
        catch (SocketException ex)
        {
            Vnc.Status = $"Connection failed: {Vnc.Host}:{Vnc.Port} ({ex.SocketErrorCode})";
        }
        catch (TimeoutException)
        {
            Vnc.Status = $"Connection timed out: {Vnc.Host}:{Vnc.Port}";
        }
        catch (Exception ex)
        {
            Vnc.Status = $"Connection failed: {ex.Message}";
        }
    }

    public async Task ConnectAsync(ConnectionProfile profile)
    {
        Vnc.Host = profile.Host;
        Vnc.Port = profile.Port;
        Vnc.Password = profile.Password;
        SelectedScale = profile.Scale <= 0 ? 1 : profile.Scale;
        SelectedProfile = Profiles.FirstOrDefault(device => device.Id == profile.DeviceId) ?? SelectedProfile;

        try
        {
            await vncClientService.ConnectAsync(Vnc.CreateOptions());
            ApplyFramebufferSizeWarning();
            FocusSimulatorRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (RfbConnectionException ex)
        {
            Vnc.Status = $"Connection failed [{ex.Code}]: {ex.Message}";
        }
        catch (SocketException ex)
        {
            Vnc.Status = $"Connection failed: {Vnc.Host}:{Vnc.Port} ({ex.SocketErrorCode})";
        }
        catch (TimeoutException)
        {
            Vnc.Status = $"Connection timed out: {Vnc.Host}:{Vnc.Port}";
        }
        catch (Exception ex)
        {
            Vnc.Status = $"Connection failed: {ex.Message}";
        }
    }

    public Task DisconnectAsync() => vncClientService.DisconnectAsync();

    private void ApplyFramebufferSizeWarning()
    {
        var framebuffer = vncClientService.Framebuffer;
        var profile = SelectedProfile;
        if (framebuffer is null || profile is null)
        {
            return;
        }

        if (framebuffer.Width != profile.ScreenWidth || framebuffer.Height != profile.ScreenHeight)
        {
            Vnc.Status = $"Connected {framebuffer.Width}x{framebuffer.Height} - warning: VNC framebuffer size != device profile screen size ({profile.ScreenWidth}x{profile.ScreenHeight})";
            return;
        }

        Vnc.Status = $"Connected {framebuffer.Width}x{framebuffer.Height}";
    }

    public void SetScale(double scale) => SelectedScale = scale;

    public async Task CaptureDeviceAsync(Control target)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            CaptureStatus = await screenshotService.CaptureDeviceAsync(target, SelectedProfile.Id);
        }
        catch (Exception ex)
        {
            CaptureStatus = $"Capture failed: {ex.Message}";
        }
    }

    public async Task SendHostKeyAsync(Key key, bool isDown)
    {
        var keysym = inputMappingService.ToKeysym(key);
        if (keysym is not null)
        {
            LogInput($"{(isDown ? "KeyDown" : "KeyUp")} {key} -> 0x{keysym.Value:x}");
            await vncClientService.SendKeyAsync(keysym.Value, isDown);
            return;
        }

        LogInput($"{(isDown ? "KeyDown" : "KeyUp")} {key} unmapped");
    }

    public async Task SendKeysymAsync(uint keysym, string source)
    {
        LogInput($"{source} -> down/up");
        await vncClientService.SendKeyAsync(keysym, true);
        await vncClientService.SendKeyAsync(keysym, false);
    }

    public async Task SendTextInputAsync(string text)
    {
        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            LogInput($"TextInput '{character}' -> 0x{(uint)character:x}");
            await vncClientService.SendKeyAsync(character, true);
            await vncClientService.SendKeyAsync(character, false);
        }
    }

    public void LogInput(string message)
    {
        LastInputStatus = $"Input: {message}";
        InputDiagnostics.Write("Input", message);
    }

    private async Task ToggleRecordingAsync()
    {
        if (recordingService.IsRecording)
        {
            await recordingService.StopRecordingAsync();
        }
        else
        {
            await recordingService.StartRecordingAsync(Path.Combine(Environment.CurrentDirectory, "captures", "placeholder.mp4"));
        }

        OnPropertyChanged(nameof(RecordingLabel));
    }
}
