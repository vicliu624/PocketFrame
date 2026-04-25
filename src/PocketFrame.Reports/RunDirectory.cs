namespace PocketFrame.Reports;

public static class RunDirectory
{
    public static string Create(string? root = null, DateTimeOffset? timestamp = null)
    {
        var stamp = (timestamp ?? DateTimeOffset.Now).ToString("yyyyMMdd_HHmmss");
        var directory = Path.GetFullPath(Path.Combine(root ?? "runs", stamp));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "screenshots"));
        return directory;
    }
}
