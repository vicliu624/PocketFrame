using PocketFrame.DeviceProfiles;

namespace PocketFrame.DeviceProfiles.Tests;

public sealed class DeviceProfileValidatorTests
{
    [Fact]
    public void ValidateRejectsScreenOutsideShell()
    {
        var shell = Path.GetTempFileName();
        try
        {
            var profile = new DeviceProfile
            {
                Id = "bad",
                Name = "Bad",
                ScreenWidth = 100,
                ScreenHeight = 100,
                ScreenX = 50,
                ScreenY = 50,
                ShellWidth = 120,
                ShellHeight = 120,
                ShellAssetPath = shell
            };

            var result = new DeviceProfileValidator().Validate(profile);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("screen geometry", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(shell);
        }
    }

    [Fact]
    public void ValidateRejectsDuplicateButtonIds()
    {
        var shell = Path.GetTempFileName();
        try
        {
            var profile = ValidProfile(shell);
            profile.Buttons.Add(new ButtonProfile { Id = "ok", Label = "OK", X = 1, Y = 1, Width = 10, Height = 10, KeyCode = "Enter" });
            profile.Buttons.Add(new ButtonProfile { Id = "ok", Label = "OK2", X = 20, Y = 1, Width = 10, Height = 10, KeyCode = "Escape" });

            var result = new DeviceProfileValidator().Validate(profile);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("unique", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(shell);
        }
    }

    private static DeviceProfile ValidProfile(string shellPath) => new()
    {
        Id = "device",
        Name = "Device",
        ScreenWidth = 100,
        ScreenHeight = 50,
        ScreenX = 10,
        ScreenY = 10,
        ShellWidth = 200,
        ShellHeight = 100,
        ShellAssetPath = shellPath
    };
}
