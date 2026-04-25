using System.Text.Json;

namespace PocketFrame.Scenarios;

public sealed class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ScenarioDefinition> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Scenario path is required.", nameof(path));
        }

        await using var stream = File.OpenRead(path);
        var scenario = await JsonSerializer.DeserializeAsync<ScenarioDefinition>(stream, JsonOptions, cancellationToken);
        return scenario ?? throw new InvalidDataException($"Invalid scenario file: {path}");
    }

    public async Task SaveAsync(ScenarioDefinition scenario, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, scenario, JsonOptions, cancellationToken);
    }
}
