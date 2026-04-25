using System.Text.Json;
using System.Text.Json.Nodes;
using PocketFrame.Automation;

var client = new AutomationPipeClient();
var server = new PocketFrameMcpServer(client);
await server.RunAsync();

internal sealed class PocketFrameMcpServer
{
    private readonly AutomationPipeClient automationClient;

    public PocketFrameMcpServer(AutomationPipeClient automationClient)
    {
        this.automationClient = automationClient;
    }

    public async Task RunAsync()
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            line = line.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                await WriteErrorAsync(null, -32700, ex.Message);
                continue;
            }

            using (document)
            {
            var root = document.RootElement;
            var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() ?? string.Empty : string.Empty;
            var hasId = root.TryGetProperty("id", out var idElement);
            var id = hasId ? idElement.Clone() : default(JsonElement?);

            try
            {
                if (!hasId)
                {
                    continue;
                }

                var result = method switch
                {
                    "initialize" => InitializeResult(),
                    "tools/list" => ToolsListResult(),
                    "tools/call" => await CallToolAsync(root.GetProperty("params")),
                    _ => throw new McpException(-32601, $"Unsupported MCP method '{method}'.")
                };

                await WriteResponseAsync(id, result);
            }
            catch (McpException ex)
            {
                await WriteErrorAsync(id, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                await WriteErrorAsync(id, -32000, ex.Message);
            }
            }
        }
    }

    private static object InitializeResult() => new
    {
        protocolVersion = "2024-11-05",
        capabilities = new
        {
            tools = new { }
        },
        serverInfo = new
        {
            name = "PocketFrame",
            version = "0.1.0"
        }
    };

    private static object ToolsListResult() => new
    {
        tools = new object[]
        {
            Tool("pocketframe_get_state", "Get the current PocketFrame device, VNC, and framebuffer state.", new JsonObject()),
            Tool("pocketframe_get_profiles", "List available PocketFrame device profiles.", new JsonObject()),
            Tool("pocketframe_get_connections", "List saved VNC connection profiles without exposing passwords.", new JsonObject()),
            Tool("pocketframe_select_device", "Select a device profile by id before connecting or running automation.", Properties(("deviceId", "string", "Device profile id.")), ["deviceId"]),
            Tool("pocketframe_set_scale", "Set the simulator display scale.", Properties(("scale", "number", "Display scale.")), ["scale"]),
            Tool("pocketframe_connect_vnc", "Connect VNC using a saved profileId or explicit host, port, password, deviceId, and scale.", Properties(("profileId", "string", "Optional saved connection id or name."), ("host", "string", "VNC host when profileId is not used."), ("port", "integer", "VNC port when profileId is not used."), ("password", "string", "Optional VNC password."), ("deviceId", "string", "Optional device profile id."), ("scale", "number", "Optional display scale."))),
            Tool("pocketframe_disconnect_vnc", "Disconnect the current VNC session.", new JsonObject()),
            Tool("pocketframe_frame_hash", "Return the current framebuffer SHA-256 hash and frame index.", new JsonObject()),
            Tool("pocketframe_capture_screen", "Capture only the remote VNC screen area to a PNG file.", Properties(("outputPath", "string", "Optional output PNG path."))),
            Tool("pocketframe_capture_device", "Capture the rendered device shell and screen to a PNG file.", Properties(("outputPath", "string", "Optional output PNG path."))),
            Tool("pocketframe_type_text", "Type text into the active VNC session. Use newline characters for Enter.", Properties(("text", "string", "Text to type.")), ["text"]),
            Tool("pocketframe_press_key", "Press a VNC key or key chord such as Enter, Escape, Ctrl+C, Up, Down, Left, Right.", Properties(("key", "string", "Key name or key chord.")), ["key"]),
            Tool("pocketframe_press_button", "Press a semantic device or virtual keyboard button such as ok, back, fn, blue, keyboard-q, keyboard-z.", Properties(("buttonId", "string", "Device button id or virtual keyboard id.")), ["buttonId"]),
            Tool("pocketframe_click_screen", "Click a coordinate in the device screen/VNC coordinate space.", Properties(("x", "integer", "Screen X coordinate."), ("y", "integer", "Screen Y coordinate."), ("button", "string", "Pointer button: left, middle, or right.")), ["x", "y"]),
            Tool("pocketframe_wait_frame_change", "Wait until the VNC framebuffer frame index changes.", Properties(("afterFrame", "integer", "Optional frame index to wait after."), ("timeoutMs", "integer", "Timeout in milliseconds."))),
            Tool("pocketframe_wait_stable_frame", "Wait until the framebuffer stops changing for a quiet window.", Properties(("quietMs", "integer", "Required quiet window in milliseconds. Defaults to 300."), ("timeoutMs", "integer", "Timeout in milliseconds. Defaults to 5000."))),
            Tool("pocketframe_action_trace", "Read, save, or clear the in-app automation action trace.", Properties(("limit", "integer", "Optional maximum number of entries."), ("outputPath", "string", "Optional JSON output path."), ("clear", "boolean", "Clear the trace after reading."))),
            Tool("pocketframe_replay_log", "Replay a saved automation action trace JSON file.", Properties(("path", "string", "Trace JSON path."), ("delayMs", "integer", "Optional delay between replayed actions.")), ["path"]),
        }
    };

    private async Task<object> CallToolAsync(JsonElement parameters)
    {
        var name = parameters.GetProperty("name").GetString() ?? string.Empty;
        var arguments = parameters.TryGetProperty("arguments", out var args) ? args : default;
        var response = name switch
        {
            "pocketframe_get_state" => await automationClient.SendAsync("get_state"),
            "pocketframe_get_profiles" => await automationClient.SendAsync("get_profiles"),
            "pocketframe_get_connections" => await automationClient.SendAsync("get_connections"),
            "pocketframe_select_device" => await automationClient.SendAsync("select_device", ToParams<SelectDeviceParams>(arguments)),
            "pocketframe_set_scale" => await automationClient.SendAsync("set_scale", ToParams<SetScaleParams>(arguments)),
            "pocketframe_connect_vnc" => await automationClient.SendAsync("connect_vnc", ToParams<ConnectVncParams>(arguments), timeoutMs: 30000),
            "pocketframe_disconnect_vnc" => await automationClient.SendAsync("disconnect_vnc"),
            "pocketframe_frame_hash" => await automationClient.SendAsync("frame_hash"),
            "pocketframe_capture_screen" => await automationClient.SendAsync("capture_screen", ToCaptureParams(arguments)),
            "pocketframe_capture_device" => await automationClient.SendAsync("capture_device", ToCaptureParams(arguments)),
            "pocketframe_type_text" => await automationClient.SendAsync("type_text", ToParams<TextInputParams>(arguments)),
            "pocketframe_press_key" => await automationClient.SendAsync("press_key", ToParams<KeyPressParams>(arguments)),
            "pocketframe_press_button" => await automationClient.SendAsync("press_button", ToParams<ButtonPressParams>(arguments)),
            "pocketframe_click_screen" => await automationClient.SendAsync("click_screen", ToParams<ClickScreenParams>(arguments)),
            "pocketframe_wait_frame_change" => await automationClient.SendAsync("wait_frame_change", ToParams<WaitFrameChangeParams>(arguments), timeoutMs: ReadTimeout(arguments)),
            "pocketframe_wait_stable_frame" => await automationClient.SendAsync("wait_stable_frame", ToParams<WaitStableFrameParams>(arguments), timeoutMs: ReadTimeout(arguments)),
            "pocketframe_action_trace" => await automationClient.SendAsync("action_trace", ToParams<ActionTraceParams>(arguments)),
            "pocketframe_replay_log" => await automationClient.SendAsync("replay_log", ToParams<ReplayLogParams>(arguments), timeoutMs: 60000),
            _ => throw new McpException(-32602, $"Unknown PocketFrame tool '{name}'.")
        };

        if (!response.Ok)
        {
            throw new McpException(-32000, $"{response.Error?.Code}: {response.Error?.Message}");
        }

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(response.Result, AutomationJson.Options)
                }
            }
        };
    }

    private static CaptureParams ToCaptureParams(JsonElement arguments) => ToParams<CaptureParams>(arguments);

    private static T ToParams<T>(JsonElement arguments)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Activator.CreateInstance<T>();
        }

        return arguments.Deserialize<T>(AutomationJson.Options) ?? Activator.CreateInstance<T>();
    }

    private static int ReadTimeout(JsonElement arguments)
    {
        if (arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty("timeoutMs", out var timeout) &&
            timeout.TryGetInt32(out var timeoutMs))
        {
            return Math.Max(timeoutMs + 1000, 2000);
        }

        return 10000;
    }

    private static object Tool(string name, string description, JsonObject properties, string[]? required = null) => new
    {
        name,
        description,
        inputSchema = new
        {
            type = "object",
            properties,
            required = required ?? []
        }
    };

    private static JsonObject Properties(params (string Name, string Type, string Description)[] properties)
    {
        var result = new JsonObject();
        foreach (var property in properties)
        {
            result[property.Name] = new JsonObject
            {
                ["type"] = property.Type,
                ["description"] = property.Description
            };
        }

        return result;
    }

    private static async Task WriteResponseAsync(JsonElement? id, object result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.HasValue ? JsonValue.Create(id.Value) : null,
            ["result"] = JsonSerializer.SerializeToNode(result, AutomationJson.Options)
        };
        await Console.Out.WriteLineAsync(response.ToJsonString(AutomationJson.Options));
        await Console.Out.FlushAsync();
    }

    private static async Task WriteErrorAsync(JsonElement? id, int code, string message)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.HasValue ? JsonValue.Create(id.Value) : null,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
        await Console.Out.WriteLineAsync(response.ToJsonString(AutomationJson.Options));
        await Console.Out.FlushAsync();
    }
}

internal sealed class McpException : Exception
{
    public McpException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
