namespace PocketFrame.DeviceProfiles;

public sealed class DeviceProfileLoadResult
{
    private DeviceProfileLoadResult(string path, DeviceProfile? profile, IReadOnlyList<string> errors)
    {
        Path = path;
        Profile = profile;
        Errors = errors;
    }

    public string Path { get; }
    public DeviceProfile? Profile { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Profile is not null && Errors.Count == 0;

    public static DeviceProfileLoadResult Valid(string path, DeviceProfile profile) => new(path, profile, []);
    public static DeviceProfileLoadResult Invalid(string path, IReadOnlyList<string> errors) => new(path, null, errors);
}
