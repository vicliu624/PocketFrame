# MCP Automation

PocketFrame can expose its running desktop simulator to AI agents through an MCP server. The MCP server does not own the VNC session or the Avalonia window. Instead, it connects to the running `PocketFrame.App` process through an internal named pipe and forwards structured automation commands to the app-side automation service.

This keeps the automation boundary explicit:

```text
AI Agent
  -> PocketFrame.Mcp over MCP stdio
  -> PocketFrame.App over an internal named pipe
  -> AutomationService
  -> VNC, device buttons, framebuffer, and capture services
```

PocketFrame does not expose an HTTP automation API in this mode.

## Projects

- `src/PocketFrame.Automation`: shared command, response, pipe, and result models.
- `src/PocketFrame.App`: starts the automation pipe server when the GUI opens.
- `src/PocketFrame.Mcp`: MCP stdio server used by AI clients.

## Start PocketFrame

Start the GUI app first:

```bash
dotnet run --project src/PocketFrame.App/PocketFrame.App.csproj
```

The automation pipe is started automatically when the main window opens. The status bar shows `Automation: MCP pipe ready` when the app-side pipe server is available.

You can connect a VNC session from the GUI, or let an MCP client prepare the app with `pocketframe_select_device`, `pocketframe_set_scale`, and `pocketframe_connect_vnc`.

## MCP Startup Model

`PocketFrame.App` starts the internal automation named pipe automatically. The MCP stdio process is normally launched by the MCP host, because stdio MCP requires the client to own the server process stdin/stdout streams.

PocketFrame release packages include both executables:

- `PocketFrame.App`, the GUI simulator and pipe server owner.
- `PocketFrame.Mcp`, the MCP stdio server launched by the AI/MCP client.

Starting a detached MCP stdio process from the GUI would not create a useful MCP session because no MCP client would be attached to its stdin/stdout. The app therefore auto-starts the automation pipe, while the MCP host starts `PocketFrame.Mcp`.

## Start the MCP Server

The MCP server is a stdio process:

```bash
dotnet run --project src/PocketFrame.Mcp/PocketFrame.Mcp.csproj
```

An MCP client should launch that command and communicate with it over stdin/stdout. The MCP process expects `PocketFrame.App` to already be running.

## Example MCP Client Configuration

Use the absolute project path that matches your checkout:

```json
{
  "mcpServers": {
    "pocketframe": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/Users/VicLi/Documents/Projects/PocketFrame/src/PocketFrame.Mcp/PocketFrame.Mcp.csproj"
      ]
    }
  }
}
```

## Tools

### `pocketframe_get_state`

Returns the current selected device, VNC status, screen size, shell size, frame index, current frame hash, and last framebuffer update time.

Arguments:

```json
{}
```

### `pocketframe_get_profiles`

Lists available device profiles.

Arguments:

```json
{}
```

### `pocketframe_get_connections`

Lists saved connection profiles without exposing stored passwords.

Arguments:

```json
{}
```

### `pocketframe_select_device`

Selects a device profile by id.

Arguments:

```json
{
  "deviceId": "cardputer-zero"
}
```

### `pocketframe_set_scale`

Sets the simulator display scale.

Arguments:

```json
{
  "scale": 1
}
```

### `pocketframe_connect_vnc`

Connects VNC either from a saved profile or explicit connection settings.

Arguments with a saved profile:

```json
{
  "profileId": "WSL Cardputer Zero"
}
```

Arguments with explicit settings:

```json
{
  "host": "127.0.0.1",
  "port": 5910,
  "password": "",
  "deviceId": "cardputer-zero",
  "scale": 1
}
```

### `pocketframe_disconnect_vnc`

Disconnects the current VNC session.

Arguments:

```json
{}
```

### `pocketframe_capture_screen`

Captures only the VNC framebuffer/screen area to a PNG file. This does not require the PocketFrame window to be visible.

Arguments:

```json
{
  "outputPath": "captures/screen.png"
}
```

If `outputPath` is omitted, PocketFrame writes a timestamped file under `captures/`.

### `pocketframe_capture_device`

Captures the rendered device shell and screen to a PNG file. This is useful for reports, visual debugging, and presentation images.

Arguments:

```json
{
  "outputPath": "captures/device.png"
}
```

If `outputPath` is omitted, PocketFrame writes a timestamped file under `captures/`.

### `pocketframe_type_text`

Types text into the connected VNC session. Newline characters are sent as Enter.

Arguments:

```json
{
  "text": "ls\n"
}
```

### `pocketframe_press_key`

Presses a VNC key or key chord.

Arguments:

```json
{
  "key": "Enter"
}
```

Examples:

```json
{ "key": "Escape" }
{ "key": "Backspace" }
{ "key": "Delete" }
{ "key": "Up" }
{ "key": "Down" }
{ "key": "Left" }
{ "key": "Right" }
{ "key": "Ctrl+C" }
```

### `pocketframe_press_button`

Presses a semantic device button or virtual keyboard button. This uses the same input mapping as the rendered device shell.

Arguments:

```json
{
  "buttonId": "ok"
}
```

Common values:

```text
ok
back
home
menu
up
down
left
right
blue
orange
fn
keyboard-q
keyboard-z
keyboard-1
keyboard-del
```

For Cardputer Zero, `blue` and `orange` are layer keys. For example, call `pocketframe_press_button` with `buttonId = "orange"` and then `buttonId = "keyboard-z"` to send the orange-layer left arrow.

### `pocketframe_click_screen`

Clicks a point in VNC screen coordinates.

Arguments:

```json
{
  "x": 120,
  "y": 80,
  "button": "left"
}
```

Coordinates are relative to the device screen framebuffer, not the host desktop window and not the rendered shell.

### `pocketframe_wait_frame_change`

Waits until the VNC framebuffer frame index changes.

Arguments:

```json
{
  "afterFrame": 42,
  "timeoutMs": 3000
}
```

If `afterFrame` is omitted, PocketFrame waits for a change after the current frame.

### `pocketframe_frame_hash`

Returns the current framebuffer SHA-256 hash and frame index. This gives agents a cheap observation fingerprint before deciding whether to capture an image.

Arguments:

```json
{}
```

### `pocketframe_wait_stable_frame`

Waits until the framebuffer stays unchanged for a quiet window. This is the preferred synchronization primitive for AI action/observation loops because it means the screen has likely settled after an input action.

Arguments:

```json
{
  "quietMs": 300,
  "timeoutMs": 5000
}
```

Example result:

```json
{
  "stable": true,
  "frameIndex": 123,
  "frameHash": "b4a8...",
  "quietMs": 300,
  "elapsedMs": 417
}
```

### `pocketframe_action_trace`

Returns the app-side automation trace. The trace records method names, arguments, timestamps, success state, frame indexes, and frame hashes before and after each action.

Arguments:

```json
{
  "limit": 100,
  "outputPath": "runs/cardputer-zero-openbox/action-trace.json",
  "clear": false
}
```

### `pocketframe_replay_log`

Replays a saved automation trace. The first version replays action-like commands and synchronization commands. Observation-only commands such as state reads and captures are ignored.

Replay results keep original and replay evidence separate. Each replayed item returns `originalEntry` and `replayEntry` so regression reports can compare the original frame hashes against the replay frame hashes without confusing the two runs.

Arguments:

```json
{
  "path": "runs/cardputer-zero-openbox/action-trace.json",
  "delayMs": 100
}
```

## Coordinate System

Automation tools use device-level coordinates:

- `click_screen.x` and `click_screen.y` are VNC framebuffer coordinates.
- Cardputer Zero uses a `320x170` screen coordinate space.
- uConsole uses a `1280x720` screen coordinate space.
- Host window position, OS display scaling, and rendered shell scale are intentionally ignored by automation commands.

## Frame Index

PocketFrame increments `frameIndex` every time a VNC framebuffer update is received. AI agents should use this value for action/observation loops:

1. Call `pocketframe_get_state`.
2. Perform an action such as `pocketframe_type_text` or `pocketframe_click_screen`.
3. Call `pocketframe_wait_frame_change` with the previous frame index.
4. Call `pocketframe_capture_screen`.
5. Inspect the returned screenshot.

For robust debugging, prefer a stable-frame loop:

1. Perform an action such as `pocketframe_type_text` or `pocketframe_click_screen`.
2. Call `pocketframe_wait_stable_frame` with `quietMs = 300`.
3. Call `pocketframe_frame_hash` or `pocketframe_capture_screen`.
4. Inspect the stable screen state.

## Error Codes

Automation responses use stable error codes so agents can branch without parsing human messages:

- `invalid_request`
- `invalid_json`
- `unsupported_method`
- `not_connected`
- `no_framebuffer`
- `timeout`
- `io_error`
- `cancelled`
- `internal_error`

## Troubleshooting

- If tools return a pipe connection error, start `PocketFrame.App` first.
- If `capture_screen` fails, connect to a VNC server first.
- Use `Tools -> Automation Inspector` in the GUI to inspect pipe status, frame index, last command, last result, and log path.
- If input logs show key events but the screen does not change, check whether the remote Linux window has focus.
- If no frame change occurs after input, use `pocketframe_capture_screen` to confirm whether the remote desktop is visually idle.
- App-side automation logs are written to `%LOCALAPPDATA%/PocketFrame/input.log` on Windows.
