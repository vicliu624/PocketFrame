namespace PocketFrame.DeviceProfiles;

public sealed class DeviceProfileValidator
{
    private static readonly HashSet<string> SupportedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Backspace", "Tab", "Enter", "Escape", "Shift", "Ctrl", "Control", "Alt", "Home",
        "Left", "Up", "Right", "Down", "F1", "F2", "F3", "F4", "F11", "F12", "Delete",
        "Space", "Fn", "Blue", "Orange"
    };

    public DeviceProfileValidationResult Validate(DeviceProfile profile)
    {
        var errors = new List<string>();
        RequireText(profile.Id, "id", errors);
        RequireText(profile.Name, "name", errors);

        if (profile.ScreenWidth <= 0)
        {
            errors.Add("screen.width must be greater than 0.");
        }

        if (profile.ScreenHeight <= 0)
        {
            errors.Add("screen.height must be greater than 0.");
        }

        if (profile.ShellWidth <= 0)
        {
            errors.Add("shell.width must be greater than 0.");
        }

        if (profile.ShellHeight <= 0)
        {
            errors.Add("shell.height must be greater than 0.");
        }

        if (profile.ScreenX < 0 || profile.ScreenY < 0 ||
            profile.ScreenX + profile.ScreenWidth > profile.ShellWidth ||
            profile.ScreenY + profile.ScreenHeight > profile.ShellHeight)
        {
            errors.Add("screen geometry must stay inside the shell bounds.");
        }

        if (string.IsNullOrWhiteSpace(profile.ShellAssetPath))
        {
            errors.Add("shell.asset is required.");
        }
        else if (!File.Exists(profile.ShellAssetPath))
        {
            errors.Add($"shell asset does not exist: {profile.ShellAssetPath}");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var button in profile.Buttons)
        {
            ValidateButton(profile, button, ids, errors);
        }

        return new DeviceProfileValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateButton(DeviceProfile profile, ButtonProfile button, HashSet<string> ids, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(button.Id))
        {
            errors.Add("button.id is required.");
        }
        else if (!ids.Add(button.Id))
        {
            errors.Add($"button.id must be unique: {button.Id}");
        }

        if (button.Width <= 0 || button.Height <= 0)
        {
            errors.Add($"button '{button.Id}' width and height must be greater than 0.");
        }

        if (button.X < 0 || button.Y < 0 ||
            button.X + button.Width > profile.ShellWidth ||
            button.Y + button.Height > profile.ShellHeight)
        {
            errors.Add($"button '{button.Id}' geometry must stay inside the shell bounds.");
        }

        ValidateKey(button.Id, "key", button.KeyCode, errors);
        ValidateOptionalKey(button.Id, "blueKey", button.BlueKeyCode, errors);
        ValidateOptionalKey(button.Id, "orangeKey", button.OrangeKeyCode, errors);
    }

    private static void ValidateKey(string buttonId, string field, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add($"button '{buttonId}' {field} is required.");
            return;
        }

        if (!IsSupportedKey(key))
        {
            errors.Add($"button '{buttonId}' {field} is not mappable: {key}");
        }
    }

    private static void ValidateOptionalKey(string buttonId, string field, string key, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(key) && !IsSupportedKey(key))
        {
            errors.Add($"button '{buttonId}' {field} is not mappable: {key}");
        }
    }

    private static bool IsSupportedKey(string key)
    {
        if (SupportedKeys.Contains(key) || key.Length == 1)
        {
            return true;
        }

        return key.StartsWith("Char:", StringComparison.Ordinal) && key.Length == 6;
    }

    private static void RequireText(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} is required.");
        }
    }
}
