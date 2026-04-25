namespace PocketFrame.Environments;

public sealed class EnvironmentProfileValidator
{
    public EnvironmentValidationResult Validate(EnvironmentProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add("Environment profile id is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add("Environment profile name is required.");
        }

        if (!EnvironmentProfileTypes.All.Contains(profile.Type, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Environment profile type is unsupported: {profile.Type}");
        }

        if (profile.Type.Equals(EnvironmentProfileTypes.Wsl, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(profile.Distro))
        {
            errors.Add("WSL environment profile distro is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.WorkingDirectory))
        {
            errors.Add("Environment profile workingDirectory is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.Shell))
        {
            errors.Add("Environment profile shell is required.");
        }

        if (profile.CommandTimeoutMs <= 0)
        {
            errors.Add("Environment profile commandTimeoutMs must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(profile.Vnc.Display) || !profile.Vnc.Display.StartsWith(':'))
        {
            errors.Add("Environment VNC display must look like :10.");
        }

        if (profile.Vnc.Port <= 0 || profile.Vnc.Port > 65535)
        {
            errors.Add("Environment VNC port must be between 1 and 65535.");
        }

        if (!VncGeometry.TryParse(profile.Vnc.Geometry, out _, out _))
        {
            errors.Add("Environment VNC geometry must look like 320x170.");
        }

        if (profile.Vnc.Depth is not (8 or 16 or 24 or 32))
        {
            errors.Add("Environment VNC depth must be 8, 16, 24, or 32.");
        }

        return new EnvironmentValidationResult(errors.Count == 0, errors);
    }
}

public sealed record EnvironmentValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class EnvironmentProfileTypes
{
    public const string Wsl = "wsl";
    public const string Local = "local";
    public static readonly string[] All = [Wsl, Local];
}

public static class VncGeometry
{
    public static bool TryParse(string value, out int width, out int height)
    {
        width = 0;
        height = 0;
        var parts = value.Split('x', 'X');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out width) &&
               int.TryParse(parts[1], out height) &&
               width > 0 &&
               height > 0;
    }
}
