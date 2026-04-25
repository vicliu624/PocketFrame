using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.DeviceProfiles;
using PocketFrame.Reports;
using PocketFrame.Runner;
using PocketFrame.Scenarios;

return await PocketFrameCli.RunAsync(args);

internal static class PocketFrameCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "devices" => await DevicesAsync(args.Skip(1).ToArray()),
                "profiles" => await ProfilesAsync(args.Skip(1).ToArray()),
                "scenario" => await ScenarioAsync(args.Skip(1).ToArray()),
                "capture" => await CaptureAsync(args.Skip(1).ToArray()),
                "trace" => await TraceAsync(args.Skip(1).ToArray()),
                "report" => await ReportAsync(args.Skip(1).ToArray()),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> DevicesAsync(string[] args)
    {
        if (args is not ["list"])
        {
            return Fail("Usage: pocketframe devices list");
        }

        var results = await new DeviceProfileLoader().LoadAsync(DefaultDevicesRoot());
        foreach (var profile in results.Where(result => result.IsValid).Select(result => result.Profile!))
        {
            Console.WriteLine($"{profile.Id}\t{profile.Name}\t{profile.ScreenWidth}x{profile.ScreenHeight}\t{profile.ShellAssetPath}");
        }

        return 0;
    }

    private static async Task<int> ProfilesAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "validate")
        {
            return Fail("Usage: pocketframe profiles validate [profile.json|devices-root]");
        }

        var target = args.Length > 1 ? args[1] : DefaultDevicesRoot();
        var loader = new DeviceProfileLoader();
        var results = File.Exists(target)
            ? [await loader.LoadFileAsync(Path.GetFullPath(target))]
            : await loader.LoadAsync(Path.GetFullPath(target));

        var failed = 0;
        foreach (var result in results)
        {
            if (result.IsValid)
            {
                Console.WriteLine($"ok: {result.Path}");
                continue;
            }

            failed++;
            Console.WriteLine($"fail: {result.Path}");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static async Task<int> ScenarioAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("Usage: pocketframe scenario validate|run scenario.json [--report]");
        }

        if (args[0] == "validate")
        {
            return await ValidateScenarioAsync(args[1]);
        }

        if (args[0] == "run")
        {
            var result = await new ScenarioRunner().RunAsync(args[1], new ScenarioRunnerOptions
            {
                GenerateReport = !args.Contains("--no-report", StringComparer.OrdinalIgnoreCase)
            });
            Console.WriteLine(JsonSerializer.Serialize(result, AutomationJson.Options));
            return result.ExitCode;
        }

        return Fail("Usage: pocketframe scenario validate|run scenario.json [--report]");
    }

    private static async Task<int> ValidateScenarioAsync(string path)
    {
        var scenario = await new ScenarioLoader().LoadAsync(path);
        var result = new ScenarioValidator().Validate(scenario);
        if (result.IsValid)
        {
            Console.WriteLine($"ok: {path}");
            return 0;
        }

        Console.WriteLine($"fail: {path}");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"  - {error}");
        }

        return 1;
    }

    private static async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("screen" or "device"))
        {
            return Fail("Usage: pocketframe capture screen|device [output.png]");
        }

        var method = args[0] == "screen" ? "capture_screen" : "capture_device";
        var outputPath = args.Length > 1 ? args[1] : null;
        var response = await new AutomationPipeClient().SendAsync(method, new CaptureParams { OutputPath = outputPath });
        return PrintAutomationResponse(response);
    }

    private static async Task<int> TraceAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Usage: pocketframe trace show|save|clear|replay ...");
        }

        var client = new AutomationPipeClient();
        var response = args[0] switch
        {
            "show" => await client.SendAsync("action_trace", new ActionTraceParams { Limit = ReadIntOption(args, "--limit") }),
            "save" when args.Length >= 2 => await client.SendAsync("action_trace", new ActionTraceParams { OutputPath = args[1] }),
            "clear" => await client.SendAsync("action_trace", new ActionTraceParams { Clear = true }),
            "replay" when args.Length >= 2 => await client.SendAsync("replay_log", new ReplayLogParams { Path = args[1], DelayMs = ReadDelay(args) }, timeoutMs: 60000),
            _ => null
        };

        return response is null ? Fail("Usage: pocketframe trace show [--limit n] | save path | clear | replay trace.json [delayMs]") : PrintAutomationResponse(response);
    }

    private static async Task<int> ReportAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "generate")
        {
            return Fail("Usage: pocketframe report generate --trace trace.json --out report.md [--scenario scenario.json]");
        }

        var tracePath = ReadStringOption(args, "--trace");
        var outPath = ReadStringOption(args, "--out");
        if (string.IsNullOrWhiteSpace(tracePath) || string.IsNullOrWhiteSpace(outPath))
        {
            return Fail("Usage: pocketframe report generate --trace trace.json --out report.md [--scenario scenario.json]");
        }

        var scenarioPath = ReadStringOption(args, "--scenario");
        var scenario = string.IsNullOrWhiteSpace(scenarioPath) ? null : await new ScenarioLoader().LoadAsync(scenarioPath);
        var trace = JsonSerializer.Deserialize<List<AutomationActionTraceEntry>>(await File.ReadAllTextAsync(tracePath), AutomationJson.Options) ?? [];
        var report = new AutomationRunReport
        {
            RunId = Path.GetFileNameWithoutExtension(outPath),
            RunDirectory = Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Environment.CurrentDirectory,
            Scenario = scenario,
            Trace = trace,
            Success = true
        };
        var path = await new MarkdownReportWriter().WriteAsync(report, outPath);
        Console.WriteLine(path);
        return 0;
    }

    private static int PrintAutomationResponse(AutomationResponse response)
    {
        if (!response.Ok)
        {
            Console.Error.WriteLine($"{response.Error?.Code}: {response.Error?.Message}");
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(response.Result, AutomationJson.Options));
        return 0;
    }

    private static string DefaultDevicesRoot() => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "PocketFrame.App", "Assets", "Devices"));

    private static int? ReadIntOption(string[] args, string name)
    {
        var value = ReadStringOption(args, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int ReadDelay(string[] args) => args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 0;

    private static string? ReadStringOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PocketFrame CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  pocketframe devices list");
        Console.WriteLine("  pocketframe profiles validate [profile.json|devices-root]");
        Console.WriteLine("  pocketframe scenario validate scenario.json");
        Console.WriteLine("  pocketframe scenario run scenario.json [--report|--no-report]");
        Console.WriteLine("  pocketframe capture screen [output.png]");
        Console.WriteLine("  pocketframe capture device [output.png]");
        Console.WriteLine("  pocketframe trace show [--limit n]");
        Console.WriteLine("  pocketframe trace save action-trace.json");
        Console.WriteLine("  pocketframe trace clear");
        Console.WriteLine("  pocketframe trace replay trace.json [delayMs]");
        Console.WriteLine("  pocketframe report generate --trace trace.json --out report.md [--scenario scenario.json]");
    }
}
