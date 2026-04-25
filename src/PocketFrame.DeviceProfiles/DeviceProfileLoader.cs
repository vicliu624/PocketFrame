using System.Text.Json;

namespace PocketFrame.DeviceProfiles;

public sealed class DeviceProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly DeviceProfileValidator validator = new();

    public async Task<IReadOnlyList<DeviceProfileLoadResult>> LoadAsync(string devicesRoot, CancellationToken cancellationToken = default)
    {
        var results = new List<DeviceProfileLoadResult>();
        if (!Directory.Exists(devicesRoot))
        {
            return results;
        }

        foreach (var file in Directory.EnumerateFiles(devicesRoot, "profile.json", SearchOption.AllDirectories))
        {
            results.Add(await LoadFileAsync(file, cancellationToken));
        }

        return results;
    }

    public async Task<DeviceProfileLoadResult> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var jsonProfile = await JsonSerializer.DeserializeAsync<JsonDeviceProfile>(stream, JsonOptions, cancellationToken);
            if (jsonProfile is null)
            {
                return DeviceProfileLoadResult.Invalid(path, ["Profile JSON is empty or invalid."]);
            }

            var profile = jsonProfile.ToDeviceProfile(Path.GetDirectoryName(path) ?? string.Empty);
            var validation = validator.Validate(profile);
            return validation.IsValid
                ? DeviceProfileLoadResult.Valid(path, profile)
                : DeviceProfileLoadResult.Invalid(path, validation.Errors);
        }
        catch (Exception ex)
        {
            return DeviceProfileLoadResult.Invalid(path, [ex.Message]);
        }
    }

    public static DeviceProfile CreateFallbackCardputerZero() => new()
    {
        Id = "cardputer-zero",
        Name = "Cardputer Zero",
        ScreenWidth = 320,
        ScreenHeight = 170,
        ShellWidth = 800,
        ShellHeight = 520,
        ScreenX = 197,
        ScreenY = 43,
        ScreenScale = 1,
        BackgroundColor = "#121722",
        Buttons =
        {
            new ButtonProfile { Id = "up", Label = "Up", X = 74, Y = 240, Width = 34, Height = 28, KeyCode = "Up" },
            new ButtonProfile { Id = "down", Label = "Down", X = 74, Y = 300, Width = 34, Height = 28, KeyCode = "Down" },
            new ButtonProfile { Id = "left", Label = "Left", X = 36, Y = 270, Width = 34, Height = 28, KeyCode = "Left" },
            new ButtonProfile { Id = "right", Label = "Right", X = 112, Y = 270, Width = 34, Height = 28, KeyCode = "Right" },
            new ButtonProfile { Id = "ok", Label = "OK", X = 356, Y = 262, Width = 44, Height = 28, KeyCode = "Enter" },
            new ButtonProfile { Id = "back", Label = "Back", X = 408, Y = 262, Width = 48, Height = 28, KeyCode = "Escape" },
            new ButtonProfile { Id = "menu", Label = "Menu", X = 356, Y = 302, Width = 48, Height = 24, KeyCode = "F1" },
            new ButtonProfile { Id = "home", Label = "Home", X = 412, Y = 302, Width = 44, Height = 24, KeyCode = "Home" }
        }
    };

    private sealed class JsonDeviceProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonScreen Screen { get; set; } = new();
        public JsonShell Shell { get; set; } = new();
        public string BackgroundColor { get; set; } = "#121722";
        public List<JsonButton> Buttons { get; set; } = [];
        public JsonKeyboard Keyboard { get; set; } = new();
        public List<JsonShellAnnotation> ShellAnnotations { get; set; } = [];

        public DeviceProfile ToDeviceProfile(string profileDirectory) => new()
        {
            Id = Id,
            Name = Name,
            ScreenWidth = Screen.Width,
            ScreenHeight = Screen.Height,
            ScreenX = Screen.X,
            ScreenY = Screen.Y,
            ScreenScale = Screen.Scale <= 0 ? 1 : Screen.Scale,
            ShellWidth = Shell.Width,
            ShellHeight = Shell.Height,
            ShellAssetPath = string.IsNullOrWhiteSpace(Shell.Asset) ? string.Empty : Path.Combine(profileDirectory, Shell.Asset),
            BackgroundColor = BackgroundColor,
            Buttons = new(Buttons.Select(button => new ButtonProfile
            {
                Id = button.Id,
                Label = button.Label,
                X = button.X,
                Y = button.Y,
                Width = button.Width,
                Height = button.Height,
                KeyCode = button.Key,
                LongPressKeyCode = button.LongPressKey,
                BlueKeyCode = button.BlueKey,
                OrangeKeyCode = button.OrangeKey,
                SymKeyCode = button.SymKey,
                FnKeyCode = button.FnKey,
                ShiftKeyCode = button.ShiftKey,
                Role = button.Role,
                Description = button.Description
            })),
            Keyboard = new KeyboardProfile
            {
                Layers = new(Keyboard.Layers),
                Keys = new(Keyboard.Keys.Select(key => new KeyProfile
                {
                    Id = key.Id,
                    X = key.X,
                    Y = key.Y,
                    Width = key.Width,
                    Height = key.Height,
                    Label = key.Label,
                    KeyCode = key.Key,
                    FnKeyCode = key.FnKey,
                    SymKeyCode = key.SymKey,
                    ShiftKeyCode = key.ShiftKey,
                    Role = key.Role,
                    Fill = key.Fill,
                    Legends = new(key.Legends.Select(legend => new KeyLegendProfile
                    {
                        Text = legend.Text,
                        Layer = legend.Layer,
                        Color = legend.Color,
                        Position = legend.Position,
                        FontSize = legend.FontSize <= 0 ? 14 : legend.FontSize,
                        Weight = legend.Weight
                    }))
                }))
            },
            ShellAnnotations = new(ShellAnnotations.Select(annotation => new ShellAnnotationProfile
            {
                Id = annotation.Id,
                Text = annotation.Text,
                X = annotation.X,
                Y = annotation.Y,
                Width = annotation.Width,
                Height = annotation.Height,
                Kind = annotation.Kind,
                Fill = annotation.Fill,
                Stroke = annotation.Stroke,
                Foreground = annotation.Foreground,
                FontSize = annotation.FontSize <= 0 ? 14 : annotation.FontSize,
                Weight = annotation.Weight,
                Radius = annotation.Radius
            }))
        };
    }

    private sealed class JsonScreen
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Scale { get; set; } = 1;
    }

    private sealed class JsonShell
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public string Asset { get; set; } = string.Empty;
    }

    private sealed class JsonButton
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Key { get; set; } = string.Empty;
        public string LongPressKey { get; set; } = string.Empty;
        public string BlueKey { get; set; } = string.Empty;
        public string OrangeKey { get; set; } = string.Empty;
        public string SymKey { get; set; } = string.Empty;
        public string FnKey { get; set; } = string.Empty;
        public string ShiftKey { get; set; } = string.Empty;
        public string Role { get; set; } = "button";
        public string Description { get; set; } = string.Empty;
    }

    private sealed class JsonKeyboard
    {
        public List<string> Layers { get; set; } = [];
        public List<JsonKey> Keys { get; set; } = [];
    }

    private sealed class JsonKey
    {
        public string Id { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string FnKey { get; set; } = string.Empty;
        public string SymKey { get; set; } = string.Empty;
        public string ShiftKey { get; set; } = string.Empty;
        public string Role { get; set; } = "key";
        public string Fill { get; set; } = string.Empty;
        public List<JsonKeyLegend> Legends { get; set; } = [];
    }

    private sealed class JsonKeyLegend
    {
        public string Text { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string Color { get; set; } = "#222222";
        public string Position { get; set; } = "topRight";
        public double FontSize { get; set; } = 14;
        public string Weight { get; set; } = "Bold";
    }

    private sealed class JsonShellAnnotation
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
        public double Radius { get; set; }
    }
}
