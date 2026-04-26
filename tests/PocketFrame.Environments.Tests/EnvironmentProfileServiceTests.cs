using PocketFrame.Environments;
using Xunit;

namespace PocketFrame.Environments.Tests;

public sealed class EnvironmentProfileServiceTests
{
    [Fact]
    public async Task ResolveTrimsDistroAndAllowsDistroLookup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "environments.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "profiles": [
                {
                  "id": "wsl-ubuntu-24.04",
                  "name": "WSL Ubuntu 24.04",
                  "type": "wsl",
                  "distro": "﻿Ubuntu-24.04​ ",
                  "workingDirectory": "~",
                  "shell": "bash",
                  "commandTimeoutMs": 30000,
                  "vnc": {
                    "display": ":10",
                    "port": 5910,
                    "geometry": "320x170",
                    "depth": 24
                  }
                }
              ]
            }
            """);

        var profile = await new EnvironmentProfileService().ResolveAsync("Ubuntu-24.04", path);

        Assert.Equal("Ubuntu-24.04", profile.Distro);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PocketFrameEnvTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
