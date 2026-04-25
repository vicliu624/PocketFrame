using PocketFrame.Environments;
using Xunit;

namespace PocketFrame.Environments.Tests;

public sealed class EnvironmentProfileValidatorTests
{
    [Fact]
    public void ValidatesDefaultWslProfile()
    {
        var profile = new EnvironmentProfile
        {
            Id = "wsl-ubuntu-24.04",
            Name = "WSL Ubuntu 24.04",
            Type = "wsl",
            Distro = "Ubuntu-24.04",
            WorkingDirectory = "~",
            Shell = "bash",
            CommandTimeoutMs = 30000,
            Vnc = new EnvironmentVncProfile
            {
                Display = ":10",
                Port = 5910,
                Geometry = "320x170",
                Depth = 24
            }
        };

        var result = new EnvironmentProfileValidator().Validate(profile);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsInvalidGeometryAndPort()
    {
        var profile = new EnvironmentProfile
        {
            Id = "bad",
            Name = "Bad",
            Type = "wsl",
            Distro = "Ubuntu-24.04",
            WorkingDirectory = "~",
            Shell = "bash",
            CommandTimeoutMs = 1,
            Vnc = new EnvironmentVncProfile
            {
                Display = "10",
                Port = 70000,
                Geometry = "wide",
                Depth = 99
            }
        };

        var result = new EnvironmentProfileValidator().Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("display", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("geometry", StringComparison.OrdinalIgnoreCase));
    }
}
