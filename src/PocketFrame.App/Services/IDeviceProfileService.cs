using PocketFrame.App.Models;

namespace PocketFrame.App.Services;

public interface IDeviceProfileService
{
    Task<IReadOnlyList<DeviceProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default);
}
