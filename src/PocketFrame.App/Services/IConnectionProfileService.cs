using PocketFrame.App.Models;

namespace PocketFrame.App.Services;

public interface IConnectionProfileService
{
    Task<IReadOnlyList<ConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IEnumerable<ConnectionProfile> profiles, CancellationToken cancellationToken = default);
}
