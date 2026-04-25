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
        ScreenWidth = 340,
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
                BlueKeyCode = button.BlueKey,
                OrangeKeyCode = button.OrangeKey,
                Description = button.Description
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
        public string BlueKey { get; set; } = string.Empty;
        public string OrangeKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
