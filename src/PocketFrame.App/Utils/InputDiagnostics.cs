using System.Diagnostics;

namespace PocketFrame.App.Utils;

public static class InputDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PocketFrame");

    private static readonly string LogPath = Path.Combine(LogDirectory, "input.log");

    public static void Write(string category, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{category}] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);

        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }
}
