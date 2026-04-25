using System.Text.Json;
using PocketFrame.Automation;
using PocketFrame.DeviceProfiles;
using PocketFrame.Environments;
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
                "connections" => await ConnectionsAsync(args.Skip(1).ToArray()),
                "environments" => await EnvironmentsAsync(args.Skip(1).ToArray()),
                "profiles" => await ProfilesAsync(args.Skip(1).ToArray()),
                "app" => await AppAsync(args.Skip(1).ToArray()),
                "scenario" => await ScenarioAsync(args.Skip(1).ToArray()),
                "capture" => await CaptureAsync(args.Skip(1).ToArray()),
                "wait" => await WaitAsync(args.Skip(1).ToArray()),
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

    private static async Task<int> ConnectionsAsync(string[] args)
    {
        if (args is not ["list"])
        {
            return Fail("Usage: pocketframe connections list");
        }

        var response = await new AutomationPipeClient().SendAsync("get_connections");
        return PrintAutomationResponse(response);
    }

    private static async Task<int> EnvironmentsAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Usage: pocketframe environments profiles|state|exec|start-vnc|stop-vnc|restart-vnc|processes|kill|read-file|write-file|install|launch|tail ...");
        }

        var client = new AutomationPipeClient();
        var response = args[0] switch
        {
            "profiles" => await client.SendAsync("environment_profiles"),
            "state" => await client.SendAsync("environment_get_state", new EnvironmentCommandParams { ProfileId = ReadStringOption(args, "--profile") ?? string.Empty }),
            "exec" => await client.SendAsync("environment_exec", new EnvironmentCommandParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Command = ReadCommandArgument(args),
                WorkingDirectory = ReadStringOption(args, "--cwd") ?? string.Empty,
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
            }, timeoutMs: (ReadIntOption(args, "--timeout") ?? 30000) + 1000),
            "start-vnc" => await client.SendAsync("environment_start_vnc", ReadEnvironmentVncParams(args), timeoutMs: (ReadIntOption(args, "--timeout") ?? 30000) + 1000),
            "stop-vnc" => await client.SendAsync("environment_stop_vnc", ReadEnvironmentVncParams(args), timeoutMs: (ReadIntOption(args, "--timeout") ?? 30000) + 1000),
            "restart-vnc" => await client.SendAsync("environment_restart_vnc", ReadEnvironmentVncParams(args), timeoutMs: (ReadIntOption(args, "--timeout") ?? 60000) + 1000),
            "processes" => await client.SendAsync("environment_processes", new EnvironmentProcessQueryParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Filter = ReadStringOption(args, "--filter") ?? string.Empty
            }),
            "kill" => await client.SendAsync("environment_kill_process", new EnvironmentKillProcessParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Pid = ReadIntOption(args, "--pid"),
                Match = ReadStringOption(args, "--match") ?? string.Empty,
                Signal = ReadStringOption(args, "--signal") ?? "TERM",
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
            }),
            "read-file" => await client.SendAsync("environment_read_file", new EnvironmentFileParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Path = ReadStringOption(args, "--path") ?? string.Empty,
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
            }),
            "write-file" => await client.SendAsync("environment_write_file", new EnvironmentFileParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Path = ReadStringOption(args, "--path") ?? string.Empty,
                Content = ReadStringOption(args, "--content") ?? string.Empty,
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
            }),
            "install" => await client.SendAsync("environment_install_packages", new EnvironmentInstallPackagesParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Packages = ReadPackageArguments(args),
                Update = args.Contains("--update", StringComparer.OrdinalIgnoreCase),
                Sudo = !args.Contains("--no-sudo", StringComparer.OrdinalIgnoreCase),
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 120000
            }, timeoutMs: (ReadIntOption(args, "--timeout") ?? 120000) + 1000),
            "launch" => await client.SendAsync("environment_launch", new EnvironmentLaunchParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Command = ReadCommandArgument(args),
                WorkingDirectory = ReadStringOption(args, "--cwd") ?? string.Empty,
                LogPath = ReadStringOption(args, "--log") ?? string.Empty,
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 10000
            }),
            "tail" => await client.SendAsync("environment_tail_file", new EnvironmentTailFileParams
            {
                ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
                Path = ReadStringOption(args, "--path") ?? string.Empty,
                Lines = ReadIntOption(args, "--lines") ?? 80,
                TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
            }),
            _ => null
        };

        return response is null ? Fail("Usage: pocketframe environments profiles|state|exec|start-vnc|stop-vnc|restart-vnc|processes|kill|read-file|write-file|install|launch|tail ...") : PrintAutomationResponse(response);
    }

    private static async Task<int> AppAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Usage: pocketframe app select-device|set-scale|connect-vnc|disconnect-vnc ...");
        }

        var client = new AutomationPipeClient();
        var response = args[0] switch
        {
            "select-device" when args.Length >= 2 => await client.SendAsync("select_device", new SelectDeviceParams { DeviceId = args[1] }),
            "set-scale" when args.Length >= 2 && double.TryParse(args[1], out var scale) => await client.SendAsync("set_scale", new SetScaleParams { Scale = scale }),
            "connect-vnc" => await client.SendAsync("connect_vnc", ReadConnectVncParams(args), timeoutMs: 30000),
            "disconnect-vnc" => await client.SendAsync("disconnect_vnc"),
            _ => null
        };

        return response is null
            ? Fail("Usage: pocketframe app select-device deviceId | set-scale scale | connect-vnc [--profile id|--host host --port port --password password --device deviceId --scale scale] | disconnect-vnc")
            : PrintAutomationResponse(response);
    }

    private static async Task<int> ScenarioAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("Usage: pocketframe scenario validate|run scenario.json [--report|--no-report] [--update-baselines] [--prepare]");
        }

        if (args[0] == "validate")
        {
            return await ValidateScenarioAsync(args[1]);
        }

        if (args[0] == "run")
        {
            var result = await new ScenarioRunner().RunAsync(args[1], new ScenarioRunnerOptions
            {
                GenerateReport = !args.Contains("--no-report", StringComparer.OrdinalIgnoreCase),
                UpdateBaselines = args.Contains("--update-baselines", StringComparer.OrdinalIgnoreCase),
                PrepareEnvironment = args.Contains("--prepare", StringComparer.OrdinalIgnoreCase)
            });
            Console.WriteLine(JsonSerializer.Serialize(result, AutomationJson.Options));
            return result.ExitCode;
        }

        return Fail("Usage: pocketframe scenario validate|run scenario.json [--report|--no-report] [--update-baselines] [--prepare]");
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

    private static async Task<int> WaitAsync(string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var durationMs))
        {
            return Fail("Usage: pocketframe wait durationMs");
        }

        var response = await new AutomationPipeClient().SendAsync(
            "wait",
            new WaitParams { DurationMs = durationMs },
            timeoutMs: Math.Max(5000, durationMs + 1000));
        return PrintAutomationResponse(response);
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

    private static ConnectVncParams ReadConnectVncParams(string[] args)
    {
        return new ConnectVncParams
        {
            ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
            Host = ReadStringOption(args, "--host") ?? string.Empty,
            Port = ReadIntOption(args, "--port") ?? 0,
            Password = ReadStringOption(args, "--password") ?? string.Empty,
            DeviceId = ReadStringOption(args, "--device") ?? string.Empty,
            Scale = ReadDoubleOption(args, "--scale") ?? 1
        };
    }

    private static double? ReadDoubleOption(string[] args, string name)
    {
        var value = ReadStringOption(args, name);
        return double.TryParse(value, out var parsed) ? parsed : null;
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
        Console.WriteLine("  pocketframe connections list");
        Console.WriteLine("  pocketframe environments profiles");
        Console.WriteLine("  pocketframe environments state [--profile id]");
        Console.WriteLine("  pocketframe environments exec -- command");
        Console.WriteLine("  pocketframe environments start-vnc|stop-vnc|restart-vnc [--profile id] [--display :10] [--geometry 320x170]");
        Console.WriteLine("  pocketframe environments processes [--filter text]");
        Console.WriteLine("  pocketframe environments kill [--pid n|--match text] [--signal TERM]");
        Console.WriteLine("  pocketframe environments read-file --path path");
        Console.WriteLine("  pocketframe environments write-file --path path --content text");
        Console.WriteLine("  pocketframe environments install [--update] -- package...");
        Console.WriteLine("  pocketframe environments launch -- command");
        Console.WriteLine("  pocketframe environments tail --path path [--lines n]");
        Console.WriteLine("  pocketframe profiles validate [profile.json|devices-root]");
        Console.WriteLine("  pocketframe app select-device deviceId");
        Console.WriteLine("  pocketframe app set-scale scale");
        Console.WriteLine("  pocketframe app connect-vnc [--profile id|--host host --port port --password password --device deviceId --scale scale]");
        Console.WriteLine("  pocketframe app disconnect-vnc");
        Console.WriteLine("  pocketframe scenario validate scenario.json");
        Console.WriteLine("  pocketframe scenario run scenario.json [--report|--no-report] [--update-baselines] [--prepare]");
        Console.WriteLine("  pocketframe capture screen [output.png]");
        Console.WriteLine("  pocketframe capture device [output.png]");
        Console.WriteLine("  pocketframe wait durationMs");
        Console.WriteLine("  pocketframe trace show [--limit n]");
        Console.WriteLine("  pocketframe trace save action-trace.json");
        Console.WriteLine("  pocketframe trace clear");
        Console.WriteLine("  pocketframe trace replay trace.json [delayMs]");
        Console.WriteLine("  pocketframe report generate --trace trace.json --out report.md [--scenario scenario.json]");
    }

    private static EnvironmentVncParams ReadEnvironmentVncParams(string[] args) => new()
    {
        ProfileId = ReadStringOption(args, "--profile") ?? string.Empty,
        Display = ReadStringOption(args, "--display") ?? string.Empty,
        Geometry = ReadStringOption(args, "--geometry") ?? string.Empty,
        Depth = ReadIntOption(args, "--depth") ?? 0,
        TimeoutMs = ReadIntOption(args, "--timeout") ?? 30000
    };

    private static string ReadCommandArgument(string[] args)
    {
        var separator = Array.IndexOf(args, "--");
        if (separator >= 0 && separator + 1 < args.Length)
        {
            return string.Join(' ', args.Skip(separator + 1));
        }

        return ReadStringOption(args, "--command") ?? string.Empty;
    }

    private static List<string> ReadPackageArguments(string[] args)
    {
        var separator = Array.IndexOf(args, "--");
        return separator >= 0
            ? args.Skip(separator + 1).Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList()
            : (ReadStringOption(args, "--packages") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
    }
}
