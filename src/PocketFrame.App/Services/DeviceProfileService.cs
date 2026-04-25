using PocketFrame.App.Utils;
using PocketFrame.DeviceProfiles;

namespace PocketFrame.App.Services;

public sealed class DeviceProfileService : IDeviceProfileService
{
    private readonly DeviceProfileLoader loader = new();

    public async Task<IReadOnlyList<DeviceProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets", "Devices");
        var results = await loader.LoadAsync(root, cancellationToken);
        var profiles = new List<DeviceProfile>();
        foreach (var result in results)
        {
            if (result.IsValid && result.Profile is not null)
            {
                profiles.Add(result.Profile);
                continue;
            }

            InputDiagnostics.Write("Profile", $"Skipped invalid profile {result.Path}: {string.Join("; ", result.Errors)}");
        }

        return profiles.Count > 0 ? profiles : [DeviceProfileLoader.CreateFallbackCardputerZero()];
    }
}
