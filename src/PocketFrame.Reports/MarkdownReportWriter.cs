using System.Text;

namespace PocketFrame.Reports;

public sealed class MarkdownReportWriter
{
    public async Task<string> WriteAsync(AutomationRunReport report, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var markdown = Build(report);
        await File.WriteAllTextAsync(path, markdown, cancellationToken);
        return Path.GetFullPath(path);
    }

    public string Build(AutomationRunReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# PocketFrame Run Report: {report.RunId}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Result: `{(report.Success ? "PASS" : "FAIL")}`");
        builder.AppendLine($"- Actions: `{report.ActionResults.Count}`");
        builder.AppendLine($"- Passed assertions: `{report.AssertionResults.Count(assertion => assertion.Passed)}`");
        builder.AppendLine($"- Failed assertions: `{report.AssertionResults.Count(assertion => !assertion.Passed)}`");
        builder.AppendLine($"- Screenshots: `{report.Screenshots.Count}`");
        builder.AppendLine($"- Errors: `{report.Errors.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Scenario");
        builder.AppendLine();
        builder.AppendLine($"- Name: `{report.Scenario?.Name ?? "unknown"}`");
        builder.AppendLine($"- Device: `{report.Scenario?.DeviceId ?? report.State?.DeviceId ?? "unknown"}`");
        builder.AppendLine($"- VNC: `{report.Scenario?.Connection.Host ?? "unknown"}:{report.Scenario?.Connection.Port.ToString() ?? "unknown"}`");
        builder.AppendLine($"- Run directory: `{report.RunDirectory}`");
        builder.AppendLine();
        builder.AppendLine("## Current State");
        builder.AppendLine();
        builder.AppendLine($"- Connected: `{report.State?.Connected.ToString() ?? "unknown"}`");
        builder.AppendLine($"- Frame index: `{report.State?.FrameIndex.ToString() ?? "unknown"}`");
        builder.AppendLine($"- Frame hash: `{report.State?.FrameHash ?? "unknown"}`");
        builder.AppendLine($"- VNC status: `{report.State?.VncStatus ?? "unknown"}`");
        builder.AppendLine();
        builder.AppendLine("## Actions");
        builder.AppendLine();
        if (report.ActionResults.Count > 0)
        {
            builder.AppendLine("| Step | Id | Type | OK | Before | After | Hash Before | Hash After | Artifact | Error |");
            builder.AppendLine("| ---: | --- | --- | --- | ---: | ---: | --- | --- | --- | --- |");
            for (var index = 0; index < report.ActionResults.Count; index++)
            {
                var action = report.ActionResults[index];
                builder.AppendLine($"| {index + 1} | `{Escape(action.Id)}` | `{Escape(action.Type)}` | `{action.Ok}` | `{action.FrameIndexBefore}` | `{action.FrameIndexAfter}` | `{ShortHash(action.FrameHashBefore)}` | `{ShortHash(action.FrameHashAfter)}` | `{Escape(action.ArtifactPath)}` | `{Escape(action.Error)}` |");
            }
        }
        else
        {
            builder.AppendLine("| Step | Method | OK | Before | After | Error |");
            builder.AppendLine("| ---: | --- | --- | ---: | ---: | --- |");
            for (var index = 0; index < report.Trace.Count; index++)
            {
                var entry = report.Trace[index];
                builder.AppendLine($"| {index + 1} | `{entry.Method}` | `{entry.Ok}` | `{entry.FrameIndexBefore}` | `{entry.FrameIndexAfter}` | `{Escape(entry.ErrorCode)}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Assertions");
        builder.AppendLine();
        builder.AppendLine("| Step | Id | Type | Result | Expected | Actual | Message |");
        builder.AppendLine("| ---: | --- | --- | --- | --- | --- | --- |");
        for (var index = 0; index < report.AssertionResults.Count; index++)
        {
            var assertion = report.AssertionResults[index];
            builder.AppendLine($"| {index + 1} | `{Escape(assertion.Id)}` | `{Escape(assertion.Type)}` | `{(assertion.Passed ? "PASS" : "FAIL")}` | `{Escape(assertion.Expected)}` | `{Escape(assertion.Actual)}` | `{Escape(assertion.Message)}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Screenshots");
        builder.AppendLine();
        foreach (var screenshot in report.Screenshots)
        {
            builder.AppendLine($"### {screenshot.Label}");
            builder.AppendLine();
            builder.AppendLine($"- Frame index: `{screenshot.FrameIndex}`");
            builder.AppendLine($"- Frame hash: `{screenshot.FrameHash}`");
            builder.AppendLine();
            builder.AppendLine($"![{screenshot.Label}]({screenshot.Path})");
            builder.AppendLine();
        }

        if (report.Errors.Count > 0)
        {
            builder.AppendLine("## Errors");
            builder.AppendLine();
            foreach (var error in report.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
    private static string ShortHash(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value[..Math.Min(value.Length, 12)];
}
