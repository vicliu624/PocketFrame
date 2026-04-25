using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PocketFrame.App.Models;
using PocketFrame.App.Services;
using PocketFrame.App.ViewModels;
using PocketFrame.App.Vnc;
using PocketFrame.Automation;
using PocketFrame.DeviceProfiles;

namespace PocketFrame.App.Automation;

public sealed class AutomationService : IAutomationService
{
    private readonly MainWindowViewModel viewModel;
    private readonly Control deviceShell;
    private readonly IVncClientService vncClientService;
    private readonly IInputMappingService inputMappingService;
    private readonly IScreenshotService screenshotService;

    public AutomationService(
        MainWindowViewModel viewModel,
        Control deviceShell,
        IVncClientService vncClientService,
        IInputMappingService inputMappingService,
        IScreenshotService screenshotService)
    {
        this.viewModel = viewModel;
        this.deviceShell = deviceShell;
        this.vncClientService = vncClientService;
        this.inputMappingService = inputMappingService;
        this.screenshotService = screenshotService;
    }

    public Task<AutomationState> GetStateAsync()
    {
        var profile = viewModel.SelectedProfile;
        return Task.FromResult(new AutomationState
        {
            Connected = viewModel.Vnc.Status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase),
            DeviceId = profile?.Id ?? string.Empty,
            DeviceName = profile?.Name ?? string.Empty,
            ScreenWidth = profile?.ScreenWidth ?? vncClientService.Framebuffer?.Width ?? 0,
            ScreenHeight = profile?.ScreenHeight ?? vncClientService.Framebuffer?.Height ?? 0,
            ShellWidth = profile?.ShellWidth ?? 0,
            ShellHeight = profile?.ShellHeight ?? 0,
            FrameIndex = vncClientService.FrameIndex,
            FrameHash = TryComputeFrameHash() ?? string.Empty,
            VncStatus = viewModel.Vnc.Status,
            LastFrameUpdatedAt = vncClientService.LastFrameUpdatedAt
        });
    }

    public async Task<DeviceProfilesResult> GetProfilesAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(() => new DeviceProfilesResult
        {
            Profiles = viewModel.Profiles.Select(profile => new DeviceProfileSummary
            {
                Id = profile.Id,
                Name = profile.Name,
                ScreenWidth = profile.ScreenWidth,
                ScreenHeight = profile.ScreenHeight,
                ShellWidth = profile.ShellWidth,
                ShellHeight = profile.ShellHeight
            }).ToList()
        });
    }

    public async Task<ConnectionProfilesResult> GetConnectionsAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(() => new ConnectionProfilesResult
        {
            Connections = viewModel.ConnectionProfiles.Select(profile => new ConnectionProfileSummary
            {
                Id = profile.Id,
                Name = profile.Name,
                Host = profile.Host,
                Port = profile.Port,
                DeviceId = profile.DeviceId,
                Scale = profile.Scale,
                HasPassword = !string.IsNullOrEmpty(profile.Password)
            }).ToList()
        });
    }

    public async Task<AutomationOperationResult> SelectDeviceAsync(string deviceId)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var profile = viewModel.Profiles.FirstOrDefault(item => item.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase)) ??
                          throw new AutomationException(AutomationErrorCodes.InvalidRequest, $"Unknown device profile '{deviceId}'.");
            viewModel.SelectedProfile = profile;
            return new AutomationOperationResult { Message = $"Selected device {profile.Id}." };
        });
    }

    public async Task<AutomationOperationResult> SetScaleAsync(double scale)
    {
        if (scale <= 0)
        {
            throw new AutomationException(AutomationErrorCodes.InvalidRequest, "Scale must be greater than 0.");
        }

        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            viewModel.SetScale(scale);
            return new AutomationOperationResult { Message = $"Scale set to {scale:0.##}." };
        });
    }

    public async Task<AutomationOperationResult> ConnectVncAsync(ConnectVncParams parameters)
    {
        var profile = await Dispatcher.UIThread.InvokeAsync(() => ResolveConnectionProfile(parameters));
        await Dispatcher.UIThread.InvokeAsync(async () => await viewModel.ConnectAsync(profile));
        if (!viewModel.Vnc.Status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationException(AutomationErrorCodes.NotConnected, viewModel.Vnc.Status);
        }

        return new AutomationOperationResult { Message = $"Connected {profile.Host}:{profile.Port} using device {profile.DeviceId}." };
    }

    public async Task<AutomationOperationResult> DisconnectVncAsync()
    {
        await viewModel.DisconnectAsync();
        return new AutomationOperationResult { Message = "Disconnected VNC." };
    }

    public Task<FrameHashResult> GetFrameHashAsync()
    {
        var framebuffer = RequireFramebuffer();
        return Task.FromResult(new FrameHashResult
        {
            FrameIndex = vncClientService.FrameIndex,
            FrameHash = ComputeFrameHash(framebuffer),
            Width = framebuffer.Width,
            Height = framebuffer.Height
        });
    }

    public async Task<CaptureResult> CaptureScreenAsync(string? outputPath)
    {
        var framebuffer = RequireFramebuffer();
        var path = ResolveCapturePath(outputPath, "screen", viewModel.SelectedProfile?.Id ?? "unknown");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveFramebufferAsync(framebuffer, path);
        return new CaptureResult
        {
            Path = path,
            Width = framebuffer.Width,
            Height = framebuffer.Height,
            FrameIndex = vncClientService.FrameIndex
        };
    }

    public async Task<CaptureResult> CaptureDeviceAsync(string? outputPath)
    {
        var profile = viewModel.SelectedProfile ?? throw new InvalidOperationException("No device profile is selected.");
        var path = await Dispatcher.UIThread.InvokeAsync(async () =>
            await screenshotService.CaptureDeviceAsync(deviceShell, profile.Id, outputPath is null ? null : Path.GetDirectoryName(Path.GetFullPath(outputPath))));

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var requestedPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(requestedPath)!);
            File.Copy(path, requestedPath, overwrite: true);
            path = requestedPath;
        }

        return new CaptureResult
        {
            Path = path,
            Width = (int)Math.Ceiling(deviceShell.Bounds.Width),
            Height = (int)Math.Ceiling(deviceShell.Bounds.Height),
            FrameIndex = vncClientService.FrameIndex
        };
    }

    public async Task TypeTextAsync(string text)
    {
        RequireConnected();
        foreach (var character in text)
        {
            if (character == '\r')
            {
                continue;
            }

            if (character == '\n')
            {
                await SendKeysymTapAsync(0xff0d);
            }
            else if (character == '\t')
            {
                await SendKeysymTapAsync(0xff09);
            }
            else if (character == '\b')
            {
                await SendKeysymTapAsync(0xff08);
            }
            else
            {
                await SendKeysymTapAsync(character);
            }
        }
    }

    public async Task PressKeyAsync(string key)
    {
        RequireConnected();
        foreach (var part in key.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("Fn", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Blue", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Orange", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
        }

        var keys = key.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (keys.Length == 0)
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var modifierKeysyms = keys[..^1].Select(ParseKey).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var mainKeysym = ParseKey(keys[^1]) ?? throw new InvalidOperationException($"Unsupported key '{keys[^1]}'.");
        foreach (var modifier in modifierKeysyms)
        {
            await vncClientService.SendKeyAsync(modifier, true);
        }

        await SendKeysymTapAsync(mainKeysym);

        foreach (var modifier in modifierKeysyms.Reverse())
        {
            await vncClientService.SendKeyAsync(modifier, false);
        }
    }

    public async Task PressButtonAsync(string buttonId)
    {
        RequireConnected();
        var button = ResolveButton(buttonId);
        await viewModel.DeviceShell.SendButtonAsync(button);
    }

    public async Task ClickScreenAsync(int x, int y, string button)
    {
        RequireConnected();
        var mask = button.ToLowerInvariant() switch
        {
            "left" => (byte)1,
            "middle" => (byte)2,
            "right" => (byte)4,
            _ => throw new InvalidOperationException($"Unsupported pointer button '{button}'.")
        };

        await vncClientService.SendPointerAsync(x, y, mask);
        await Task.Delay(60);
        await vncClientService.SendPointerAsync(x, y, 0);
    }

    public async Task<WaitFrameResult> WaitFrameChangeAsync(long? afterFrame, int timeoutMs)
    {
        RequireFramebuffer();
        var previous = afterFrame ?? vncClientService.FrameIndex;
        if (vncClientService.FrameIndex > previous)
        {
            return new WaitFrameResult { Changed = true, PreviousFrameIndex = previous, CurrentFrameIndex = vncClientService.FrameIndex };
        }

        var completion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrame(object? sender, RfbFramebuffer framebuffer)
        {
            if (vncClientService.FrameIndex > previous)
            {
                completion.TrySetResult(vncClientService.FrameIndex);
            }
        }

        vncClientService.FramebufferUpdated += OnFrame;
        try
        {
            var completed = await Task.WhenAny(completion.Task, Task.Delay(Math.Max(timeoutMs, 1)));
            var current = completed == completion.Task ? await completion.Task : vncClientService.FrameIndex;
            return new WaitFrameResult
            {
                Changed = current > previous,
                PreviousFrameIndex = previous,
                CurrentFrameIndex = current
            };
        }
        finally
        {
            vncClientService.FramebufferUpdated -= OnFrame;
        }
    }

    public async Task<WaitStableFrameResult> WaitStableFrameAsync(int quietMs, int timeoutMs)
    {
        quietMs = Math.Max(quietMs, 1);
        timeoutMs = Math.Max(timeoutMs, 1);
        var start = Environment.TickCount64;
        var lastHash = (await GetFrameHashAsync()).FrameHash;
        var lastFrameIndex = vncClientService.FrameIndex;
        var stableSince = Environment.TickCount64;

        while (Environment.TickCount64 - start <= timeoutMs)
        {
            await Task.Delay(Math.Min(quietMs, 100));
            var current = await GetFrameHashAsync();
            if (!string.Equals(current.FrameHash, lastHash, StringComparison.Ordinal) ||
                current.FrameIndex != lastFrameIndex)
            {
                lastHash = current.FrameHash;
                lastFrameIndex = current.FrameIndex;
                stableSince = Environment.TickCount64;
                continue;
            }

            if (Environment.TickCount64 - stableSince >= quietMs)
            {
                return new WaitStableFrameResult
                {
                    Stable = true,
                    FrameIndex = current.FrameIndex,
                    FrameHash = current.FrameHash,
                    QuietMs = quietMs,
                    ElapsedMs = (int)(Environment.TickCount64 - start)
                };
            }
        }

        var latest = await GetFrameHashAsync();
        return new WaitStableFrameResult
        {
            Stable = false,
            FrameIndex = latest.FrameIndex,
            FrameHash = latest.FrameHash,
            QuietMs = quietMs,
            ElapsedMs = (int)(Environment.TickCount64 - start)
        };
    }

    private async Task SendKeysymTapAsync(uint keysym)
    {
        await vncClientService.SendKeyAsync(keysym, true);
        await vncClientService.SendKeyAsync(keysym, false);
    }

    private void RequireConnected()
    {
        if (!viewModel.Vnc.Status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationException(AutomationErrorCodes.NotConnected, "VNC is not connected.");
        }
    }

    private RfbFramebuffer RequireFramebuffer()
    {
        return vncClientService.Framebuffer ??
               throw new AutomationException(AutomationErrorCodes.NoFramebuffer, "No VNC framebuffer is available.");
    }

    private string? TryComputeFrameHash()
    {
        var framebuffer = vncClientService.Framebuffer;
        return framebuffer is null ? null : ComputeFrameHash(framebuffer);
    }

    private static string ComputeFrameHash(RfbFramebuffer framebuffer)
    {
        var snapshot = framebuffer.Snapshot();
        var hash = SHA256.HashData(snapshot);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private uint? ParseKey(string key)
    {
        if (key.StartsWith("Char:", StringComparison.Ordinal) && key.Length == 6)
        {
            return key[5];
        }

        if (key.Length == 1)
        {
            return key[0];
        }

        return inputMappingService.ToKeysym(key);
    }

    private ButtonProfile ResolveButton(string buttonId)
    {
        var profileButton = viewModel.SelectedProfile?.Buttons.FirstOrDefault(button =>
            button.Id.Equals(buttonId, StringComparison.OrdinalIgnoreCase) ||
            button.Label.Equals(buttonId, StringComparison.OrdinalIgnoreCase));
        if (profileButton is not null)
        {
            return profileButton;
        }

        var virtualButton = ResolveCardputerKeyboardButton(buttonId) ?? ResolveGenericKeyboardButton(buttonId);
        if (virtualButton is not null)
        {
            return virtualButton;
        }

        throw new InvalidOperationException($"Unknown device button '{buttonId}'.");
    }

    private static ButtonProfile? ResolveGenericKeyboardButton(string buttonId)
    {
        var normalized = NormalizeButtonId(buttonId);
        var keyCode = normalized switch
        {
            "enter" or "ok" => "Enter",
            "escape" or "back" => "Escape",
            "backspace" => "Backspace",
            "delete" => "Delete",
            "tab" => "Tab",
            "space" => "Space",
            "left" => "Left",
            "right" => "Right",
            "up" => "Up",
            "down" => "Down",
            "blue" => "Blue",
            "orange" or "fn" => "Orange",
            _ when normalized.Length == 1 => normalized,
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(keyCode)
            ? null
            : new ButtonProfile { Id = normalized, Label = normalized, KeyCode = keyCode };
    }

    private static ButtonProfile? ResolveCardputerKeyboardButton(string buttonId)
    {
        var normalized = NormalizeButtonId(buttonId);
        var key = normalized.StartsWith("keyboard-", StringComparison.Ordinal) ? normalized["keyboard-".Length..] : normalized;
        return key switch
        {
            "blue" or "aa" => new ButtonProfile { Id = "keyboard-blue", Label = "Aa", KeyCode = "Blue" },
            "orange" or "fn" => new ButtonProfile { Id = "keyboard-orange", Label = "fn", KeyCode = "Orange" },
            "del" => new ButtonProfile { Id = "keyboard-del", Label = "del", KeyCode = "Backspace", OrangeKeyCode = "Delete" },
            "comma" => new ButtonProfile { Id = "keyboard-comma", Label = ",", KeyCode = ",", BlueKeyCode = "." },
            _ when key.Length == 1 => CreateCardputerLetterOrDigitButton(key),
            _ => null
        };
    }

    private static ButtonProfile CreateCardputerLetterOrDigitButton(string key)
    {
        var blue = key switch
        {
            "1" => "!",
            "2" => "@",
            "3" => "#",
            "4" => "$",
            "5" => "%",
            "6" => "^",
            "7" => "&",
            "8" => "*",
            "9" => "(",
            "0" => ")",
            "q" => "~",
            "w" => "`",
            "e" => "+",
            "r" => "-",
            "t" => "/",
            "y" => "\\",
            "u" => "{",
            "i" => "}",
            "o" => "[",
            "p" => "]",
            "f" => "|",
            "g" => "=",
            "h" => ":",
            "j" => ";",
            "k" => "_",
            "l" => "?",
            "v" => "<",
            "b" => ">",
            "n" => "'",
            "m" => "\"",
            _ => string.Empty
        };

        var orange = key switch
        {
            "d" => "Up",
            "z" => "Left",
            "x" => "Down",
            "c" => "Right",
            _ => string.Empty
        };

        return new ButtonProfile
        {
            Id = $"keyboard-{key}",
            Label = key,
            KeyCode = key,
            BlueKeyCode = blue,
            OrangeKeyCode = orange
        };
    }

    private static string NormalizeButtonId(string value) => value.Trim().ToLowerInvariant();

    private static string ResolveCapturePath(string? outputPath, string captureKind, string deviceId)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.GetFullPath(outputPath);
        }

        var directory = Path.Combine(Environment.CurrentDirectory, "captures");
        return Path.Combine(directory, $"PocketFrame_{deviceId}_{captureKind}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    private ConnectionProfile ResolveConnectionProfile(ConnectVncParams parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.ProfileId))
        {
            var saved = viewModel.ConnectionProfiles.FirstOrDefault(profile =>
                profile.Id.Equals(parameters.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                profile.Name.Equals(parameters.ProfileId, StringComparison.OrdinalIgnoreCase));
            if (saved is null)
            {
                throw new AutomationException(AutomationErrorCodes.InvalidRequest, $"Unknown connection profile '{parameters.ProfileId}'.");
            }

            return saved;
        }

        if (string.IsNullOrWhiteSpace(parameters.Host))
        {
            throw new AutomationException(AutomationErrorCodes.InvalidRequest, "connect_vnc requires profileId or host.");
        }

        if (parameters.Port <= 0 || parameters.Port > 65535)
        {
            throw new AutomationException(AutomationErrorCodes.InvalidRequest, "connect_vnc port must be between 1 and 65535.");
        }

        return new ConnectionProfile
        {
            Name = "Automation",
            Host = parameters.Host,
            Port = parameters.Port,
            Password = parameters.Password,
            DeviceId = string.IsNullOrWhiteSpace(parameters.DeviceId) ? viewModel.SelectedProfile?.Id ?? "cardputer-zero" : parameters.DeviceId,
            Scale = parameters.Scale <= 0 ? 1 : parameters.Scale
        };
    }

    private static async Task SaveFramebufferAsync(RfbFramebuffer framebuffer, string path)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(framebuffer.Width, framebuffer.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        using (var locked = bitmap.Lock())
        {
            Marshal.Copy(framebuffer.Snapshot(), 0, locked.Address, framebuffer.Width * framebuffer.Height * 4);
        }

        await using var stream = File.Create(path);
        bitmap.Save(stream);
    }
}
