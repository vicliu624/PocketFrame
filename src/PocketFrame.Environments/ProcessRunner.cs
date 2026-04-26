using System.Diagnostics;
using System.Text;

namespace PocketFrame.Environments;

internal sealed class ProcessRunner
{
    public async Task<EnvironmentCommandResult> RunAsync(
        string fileName,
        string arguments,
        string logicalCommand,
        string workingDirectory,
        string stdin,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        return await RunAsync(fileName, splitArguments: null, arguments, logicalCommand, workingDirectory, stdin, timeoutMs, cancellationToken);
    }

    public async Task<EnvironmentCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string logicalCommand,
        string workingDirectory,
        string stdin,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        return await RunAsync(fileName, arguments, flatArguments: null, logicalCommand, workingDirectory, stdin, timeoutMs, cancellationToken);
    }

    private async Task<EnvironmentCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string>? splitArguments,
        string? flatArguments,
        string logicalCommand,
        string workingDirectory,
        string stdin,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.Now;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = splitArguments is null ? flatArguments ?? string.Empty : string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (splitArguments is not null)
        {
            foreach (var argument in splitArguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
        }

        var result = new EnvironmentCommandResult
        {
            Command = logicalCommand,
            WorkingDirectory = workingDirectory
        };

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            if (!string.IsNullOrEmpty(stdin))
            {
                await process.StandardInput.WriteAsync(stdin.AsMemory(), timeout.Token);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            result.ExitCode = process.ExitCode;
            result.Stdout = Limit(await stdoutTask);
            result.Stderr = Limit(await stderrTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.TimedOut = true;
            result.ExitCode = -1;
            result.Stderr = $"Command timed out after {timeoutMs} ms.";
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        result.DurationMs = (int)(DateTimeOffset.Now - start).TotalMilliseconds;
        return result;
    }

    private static string Limit(string value)
    {
        const int maxLength = 128 * 1024;
        return value.Length <= maxLength ? value : value[..maxLength] + "\n...[truncated]";
    }
}
