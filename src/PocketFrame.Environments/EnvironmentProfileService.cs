using System.Text.Json;

namespace PocketFrame.Environments;

public sealed class EnvironmentProfileService
{
    private readonly EnvironmentProfileValidator validator = new();

    public async Task<List<EnvironmentProfileLoadResult>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            var fallback = DefaultProfile();
            return [new EnvironmentProfileLoadResult(path, fallback, validator.Validate(fallback).Errors)];
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<EnvironmentProfilesDocument>(stream, EnvironmentJson.Options, cancellationToken) ?? new();
        var results = new List<EnvironmentProfileLoadResult>();
        foreach (var profile in document.Profiles)
        {
            Normalize(profile);
            var validation = validator.Validate(profile);
            results.Add(new EnvironmentProfileLoadResult(path, profile, validation.Errors));
        }

        if (results.Count == 0)
        {
            results.Add(new EnvironmentProfileLoadResult(path, null, ["No environment profiles were found."]));
        }

        return results;
    }

    public async Task<EnvironmentProfile> ResolveAsync(string profileId, string? path = null, CancellationToken cancellationToken = default)
    {
        var results = await LoadAsync(path ?? DefaultProfilePath(), cancellationToken);
        var valid = results.Where(result => result.IsValid && result.Profile is not null).Select(result => result.Profile!).ToList();
        var selected = string.IsNullOrWhiteSpace(profileId)
            ? valid.FirstOrDefault()
            : valid.FirstOrDefault(profile => profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase) ||
                                               profile.Name.Equals(profileId, StringComparison.OrdinalIgnoreCase) ||
                                               profile.Distro.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        return selected ?? throw new InvalidOperationException($"Unknown environment profile '{profileId}'.");
    }

    public async Task<EnvironmentProfilesResult> ListAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var results = await LoadAsync(path ?? DefaultProfilePath(), cancellationToken);
        return new EnvironmentProfilesResult
        {
            Profiles = results
                .Where(result => result.IsValid && result.Profile is not null)
                .Select(result => result.Profile!)
                .Select(profile => new EnvironmentProfileSummary
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    Type = profile.Type,
                    Distro = profile.Distro,
                    WorkingDirectory = profile.WorkingDirectory,
                    VncDisplay = profile.Vnc.Display,
                    VncPort = profile.Vnc.Port,
                    VncGeometry = profile.Vnc.Geometry
                })
                .ToList()
        };
    }

    public static string DefaultProfilePath() => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "environments.json"));

    private static void Normalize(EnvironmentProfile profile)
    {
        profile.Id = Clean(profile.Id);
        profile.Name = Clean(profile.Name);
        profile.Type = Clean(profile.Type);
        profile.Distro = Clean(profile.Distro);
        profile.WorkingDirectory = Clean(profile.WorkingDirectory);
        profile.Shell = Clean(profile.Shell);
        profile.Vnc.Display = Clean(profile.Vnc.Display);
        profile.Vnc.Host = Clean(profile.Vnc.Host);
        profile.Vnc.Geometry = Clean(profile.Vnc.Geometry);
        profile.Vnc.StartupCommand = profile.Vnc.StartupCommand.Trim();
    }

    private static string Clean(string value) =>
        value.Trim().Trim('\uFEFF', '\u200B', '\u200C', '\u200D');

    private static EnvironmentProfile DefaultProfile() => new()
    {
        Id = "wsl-ubuntu-24.04",
        Name = "WSL Ubuntu 24.04",
        Type = EnvironmentProfileTypes.Wsl,
        Distro = "Ubuntu-24.04",
        WorkingDirectory = "~",
        Shell = "bash",
        CommandTimeoutMs = 30000,
        Vnc = new EnvironmentVncProfile
        {
            Display = ":10",
            Host = "127.0.0.1",
            Port = 5910,
            Geometry = "320x170",
            Depth = 24,
            StartupCommand = "openbox & xterm"
        }
    };
}

public sealed record EnvironmentProfileLoadResult(string Path, EnvironmentProfile? Profile, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0 && Profile is not null;
}

public static class EnvironmentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
