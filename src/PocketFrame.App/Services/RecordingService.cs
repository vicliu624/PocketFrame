namespace PocketFrame.App.Services;

public sealed class RecordingService : IRecordingService
{
    public bool IsRecording { get; private set; }
    public string? OutputPath { get; private set; }

    public Task StartRecordingAsync(string outputPath)
    {
        OutputPath = outputPath;
        IsRecording = true;
        return Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        IsRecording = false;
        return Task.CompletedTask;
    }
}
