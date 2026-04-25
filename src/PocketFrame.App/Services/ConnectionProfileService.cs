using System.Text.Json;
using PocketFrame.App.Models;

namespace PocketFrame.App.Services;

public sealed class ConnectionProfileService : IConnectionProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PocketFrame",
        "connections.json");

    public async Task<IReadOnlyList<ConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return
            [
                new ConnectionProfile { Name = "WSL Cardputer Zero", Host = "127.0.0.1", Port = 5910, DeviceId = "cardputer-zero", Scale = 1 },
                new ConnectionProfile { Name = "WSL uConsole", Host = "127.0.0.1", Port = 5911, DeviceId = "uconsole", Scale = 1 }
            ];
        }

        await using var stream = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<List<ConnectionProfile>>(stream, cancellationToken: cancellationToken) ?? [];
    }

    public async Task SaveAsync(IEnumerable<ConnectionProfile> profiles, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, profiles.ToList(), JsonOptions, cancellationToken);
    }
}
