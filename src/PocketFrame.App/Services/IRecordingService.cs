namespace PocketFrame.App.Services;

public interface IRecordingService
{
    bool IsRecording { get; }
    string? OutputPath { get; }
    Task StartRecordingAsync(string outputPath);
    Task StopRecordingAsync();
}
