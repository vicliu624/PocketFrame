using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.Scenarios;

return await PocketFrameCli.RunAsync(args);

internal static class PocketFrameCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "devices" => await DevicesAsync(args.Skip(1).ToArray()),
                "profiles" => await ProfilesAsync(args.Skip(1).ToArray()),
                "scenario" => await ScenarioAsync(args.Skip(1).ToArray()),
                "capture" => await CaptureAsync(args.Skip(1).ToArray()),
                "trace" => await TraceAsync(args.Skip(1).ToArray()),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static Task<int> DevicesAsync(string[] args)
    {
        if (args is not ["list"])
        {
            return Task.FromResult(Fail("Usage: pocketframe devices list"));
        }

        foreach (var profile in FindProfileFiles().Select(ReadProfileSummary))
        {
            Console.WriteLine($"{profile.Id}\t{profile.Name}\t{profile.ScreenWidth}x{profile.ScreenHeight}\t{profile.Path}");
        }

        return Task.FromResult(0);
    }

    private static Task<int> ProfilesAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "validate")
        {
            return Task.FromResult(Fail("Usage: pocketframe profiles validate [profile.json|devices-root]"));
        }

        var target = args.Length > 1 ? args[1] : DefaultDevicesRoot();
        var files = File.Exists(target) ? [Path.GetFullPath(target)] : Directory.EnumerateFiles(target, "profile.json", SearchOption.AllDirectories).ToArray();
        var failed = 0;
        foreach (var file in files)
        {
            var result = ValidateProfile(file);
            if (result.Count == 0)
            {
                Console.WriteLine($"ok: {file}");
            }
            else
            {
                failed++;
                Console.WriteLine($"fail: {file}");
                foreach (var error in result)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        }

        return Task.FromResult(failed == 0 ? 0 : 1);
    }

    private static async Task<int> ScenarioAsync(string[] args)
    {
        if (args.Length < 2 || args[0] != "validate")
        {
            return Fail("Usage: pocketframe scenario validate scenario.json");
        }

        var scenario = await new ScenarioLoader().LoadAsync(args[1]);
        var result = new ScenarioValidator().Validate(scenario);
        if (result.IsValid)
        {
            Console.WriteLine($"ok: {args[1]}");
            return 0;
        }

        Console.WriteLine($"fail: {args[1]}");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"  - {error}");
        }

        return 1;
    }

    private static async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("screen" or "device"))
        {
            return Fail("Usage: pocketframe capture screen|device [output.png]");
        }

        var method = args[0] == "screen" ? "capture_screen" : "capture_device";
        var outputPath = args.Length > 1 ? args[1] : null;
        var response = await new AutomationPipeClient().SendAsync(method, new CaptureParams { OutputPath = outputPath });
        return PrintAutomationResponse(response);
    }

    private static async Task<int> TraceAsync(string[] args)
    {
        if (args.Length < 2 || args[0] != "replay")
        {
            return Fail("Usage: pocketframe trace replay trace.json [delayMs]");
        }

        var delayMs = args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 0;
        var response = await new AutomationPipeClient().SendAsync("replay_log", new ReplayLogParams { Path = args[1], DelayMs = delayMs }, timeoutMs: 60000);
        return PrintAutomationResponse(response);
    }

    private static int PrintAutomationResponse(AutomationResponse response)
    {
        if (!response.Ok)
        {
            Console.Error.WriteLine($"{response.Error?.Code}: {response.Error?.Message}");
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(response.Result, AutomationJson.Options));
        return 0;
    }

    private static IReadOnlyList<string> ValidateProfile(string file)
    {
        var errors = new List<string>();
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        RequireString(root, "id", errors);
        RequireString(root, "name", errors);
        var screen = RequireObject(root, "screen", errors);
        var shell = RequireObject(root, "shell", errors);
        var screenWidth = RequirePositiveInt(screen, "width", "screen.width", errors);
        var screenHeight = RequirePositiveInt(screen, "height", "screen.height", errors);
        var screenX = RequireNumber(screen, "x", "screen.x", errors);
        var screenY = RequireNumber(screen, "y", "screen.y", errors);
        var shellWidth = RequirePositiveNumber(shell, "width", "shell.width", errors);
        var shellHeight = RequirePositiveNumber(shell, "height", "shell.height", errors);
        var shellAsset = RequireString(shell, "asset", errors);
        if (!string.IsNullOrWhiteSpace(shellAsset))
        {
            var assetPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!, shellAsset);
            if (!File.Exists(assetPath))
            {
                errors.Add($"shell asset does not exist: {assetPath}");
            }
        }

        if (screenX < 0 || screenY < 0 || screenX + screenWidth > shellWidth || screenY + screenHeight > shellHeight)
        {
            errors.Add("screen geometry must stay inside shell bounds.");
        }

        if (root.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var button in buttons.EnumerateArray())
            {
                var id = RequireString(button, "id", errors);
                if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                {
                    errors.Add($"button.id must be unique: {id}");
                }

                var buttonX = RequireNumber(button, "x", $"button '{id}' x", errors);
                var buttonY = RequireNumber(button, "y", $"button '{id}' y", errors);
                var buttonWidth = RequirePositiveNumber(button, "width", $"button '{id}' width", errors);
                var buttonHeight = RequirePositiveNumber(button, "height", $"button '{id}' height", errors);
                var key = RequireString(button, "key", errors);
                ValidateMappableKey(id, "key", key, errors);
                ValidateOptionalMappableKey(id, "blueKey", button, errors);
                ValidateOptionalMappableKey(id, "orangeKey", button, errors);
                if (buttonX < 0 || buttonY < 0 || buttonX + buttonWidth > shellWidth || buttonY + buttonHeight > shellHeight)
                {
                    errors.Add($"button '{id}' geometry must stay inside shell bounds.");
                }
            }
        }

        return errors;
    }

    private static ProfileSummary ReadProfileSummary(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        var screen = root.GetProperty("screen");
        return new ProfileSummary(
            root.GetProperty("id").GetString() ?? string.Empty,
            root.GetProperty("name").GetString() ?? string.Empty,
            screen.GetProperty("width").GetInt32(),
            screen.GetProperty("height").GetInt32(),
            file);
    }

    private static string[] FindProfileFiles()
    {
        var root = DefaultDevicesRoot();
        return Directory.Exists(root) ? Directory.EnumerateFiles(root, "profile.json", SearchOption.AllDirectories).ToArray() : [];
    }

    private static string DefaultDevicesRoot() => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "PocketFrame.App", "Assets", "Devices"));

    private static JsonElement RequireObject(JsonElement root, string name, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{name} is required.");
            return default;
        }

        return value;
    }

    private static string RequireString(JsonElement root, string name, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            errors.Add($"{name} is required.");
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private static int RequirePositiveInt(JsonElement root, string name, string label, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result) || result <= 0)
        {
            errors.Add($"{label} must be greater than 0.");
            return 0;
        }

        return result;
    }

    private static double RequirePositiveNumber(JsonElement root, string name, string label, List<string> errors)
    {
        var value = RequireNumber(root, name, label, errors);
        if (value <= 0)
        {
            errors.Add($"{label} must be greater than 0.");
        }

        return value;
    }

    private static double RequireNumber(JsonElement root, string name, string label, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetDouble(out var result))
        {
            errors.Add($"{label} is required.");
            return 0;
        }

        return result;
    }

    private static void ValidateOptionalMappableKey(string buttonId, string property, JsonElement button, List<string> errors)
    {
        if (button.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            ValidateMappableKey(buttonId, property, value.GetString() ?? string.Empty, errors);
        }
    }

    private static void ValidateMappableKey(string buttonId, string property, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (key.Length == 1 || (key.StartsWith("Char:", StringComparison.Ordinal) && key.Length == 6))
        {
            return;
        }

        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Backspace", "Tab", "Enter", "Escape", "Shift", "Ctrl", "Control", "Alt", "Home",
            "Left", "Up", "Right", "Down", "F1", "F2", "F3", "F4", "F11", "F12", "Delete",
            "Space", "Fn", "Blue", "Orange"
        };

        if (!named.Contains(key))
        {
            errors.Add($"button '{buttonId}' {property} is not mappable: {key}");
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PocketFrame CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  pocketframe devices list");
        Console.WriteLine("  pocketframe profiles validate [profile.json|devices-root]");
        Console.WriteLine("  pocketframe scenario validate scenario.json");
        Console.WriteLine("  pocketframe capture screen [output.png]");
        Console.WriteLine("  pocketframe capture device [output.png]");
        Console.WriteLine("  pocketframe trace replay trace.json [delayMs]");
    }

    private sealed record ProfileSummary(string Id, string Name, int ScreenWidth, int ScreenHeight, string Path);
}
