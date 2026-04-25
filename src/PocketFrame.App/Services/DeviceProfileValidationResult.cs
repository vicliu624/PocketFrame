namespace PocketFrame.App.Services;

public sealed class DeviceProfileValidationResult
{
    public DeviceProfileValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }
}
