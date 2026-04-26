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

For AI-driven debugging, MCP can also prepare the target Linux environment through `pocketframe_environment_*` tools. These tools are routed through `PocketFrame.App`, so the GUI activity panel and run reports can show what the AI did outside the framebuffer.

## Target Environment Tools

Environment tools are adapter-based. The default adapter is WSL, but the MCP schema describes target environment operations rather than WSL-specific commands.

- `pocketframe_environment_profiles`
- `pocketframe_environment_get_state`
- `pocketframe_environment_exec`
- `pocketframe_environment_start_vnc`
- `pocketframe_environment_stop_vnc`
- `pocketframe_environment_restart_vnc`
- `pocketframe_environment_processes`
- `pocketframe_environment_kill_process`
- `pocketframe_environment_read_file`
- `pocketframe_environment_write_file`
- `pocketframe_environment_install_packages`
- `pocketframe_environment_launch`
- `pocketframe_environment_tail_file`
- `pocketframe_environment_app_status`
- `pocketframe_environment_app_kill`
- `pocketframe_environment_app_launch`
- `pocketframe_environment_app_clean_state`
- `pocketframe_environment_app_tail_log`
- `pocketframe_environment_input_devices`
- `pocketframe_environment_evdev_capture`

App lifecycle tools use an app profile shape on top of the environment adapter. They are intended to prevent stale target processes, stale state/cache directories, and stale logs from polluting an AI debugging run.

Example app profile payload:

```json
{
  "profileId": "wsl-ubuntu-24.04",
  "id": "lofibox",
  "processMatch": "lofibox",
  "binaryPath": "/home/user/lofibox/target/debug/lofibox",
  "command": "DISPLAY=:10 ./lofibox",
  "workingDirectory": "/home/user/lofibox",
  "clearPaths": [".tmp/state", ".tmp/cache"],
  "env": {
    "XDG_STATE_HOME": ".tmp/state",
    "XDG_CACHE_HOME": ".tmp/cache",
    "LOFIBOX_RUNTIME_LOG_PATH": ".tmp/lofibox.log"
  },
  "logPath": ".tmp/lofibox.log",
  "killBeforeLaunch": true
}
```

`pocketframe_environment_app_status` returns whether the matched app is running plus PID, cwd, command line, binary mtime, log path, and state directory when available.

`pocketframe_environment_input_devices` and `pocketframe_environment_evdev_capture` observe target Linux evdev devices. They are intentionally separate from PocketFrame input injection. VNC-injected keys may not appear in evdev captures.

## Input Model Tools

Use `pocketframe_get_input_model` when an agent needs to understand a device keyboard instead of guessing physical shortcuts. It returns physical buttons, `shortPressKey`, optional `longPressKey`, `supportsLongPress`, keyboard keys, coordinates, layers, and normal/Fn/SYM/Shift actions.

Use `pocketframe_get_keyboard_state` to inspect currently latched layers such as `fn`, `sym`, `shift`, `ctrl`, or `alt`.

Example VNC restart:

```json
{
  "profileId": "wsl-ubuntu-24.04",
  "display": ":10",
  "geometry": "320x170",
  "depth": 24
}
```

Example package install:

```json
{
  "profileId": "wsl-ubuntu-24.04",
  "packages": ["xterm", "openbox"],
  "update": true
}
```

Example app launch:

```json
{
  "profileId": "wsl-ubuntu-24.04",
  "command": "DISPLAY=:10 xterm",
  "logPath": "~/pocketframe-xterm.log"
}
```

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

Presses a VNC logical key or key chord. This is useful for validating application behavior through the VNC/X11 input path. It is not Linux evdev `KEY_*` truth.

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

Returns structured input evidence:

```json
{
  "ok": true,
  "requestedAction": "press_key",
  "requestedKey": "Left",
  "inputLayer": "vnc-logical-key",
  "resolvedKey": "Left",
  "emittedTransport": "vnc",
  "emittedKey": "Left",
  "emittedKeysym": "0xff51",
  "warnings": [
    "press_key sends a VNC logical key event.",
    "VNC key events are not Linux evdev events."
  ]
}
```

### `pocketframe_press_button`

Presses or holds a semantic device button or virtual keyboard button. This uses the same input mapping as the rendered device shell. It is a PocketFrame profile projection, not target Linux evdev truth.

Arguments:

```json
{
  "buttonId": "ok",
  "durationMs": 0
}
```

`durationMs` is optional. Omit it or use `0` for a short press. Use a positive duration to hold the button. For hardware-style long-press behavior, use at least the same threshold as the GUI interaction, typically `420` ms or more.

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

Cardputer Zero physical buttons can also expose separate short-press and long-press behavior through the input model. For example:

```json
{ "buttonId": "next-home" }
{ "buttonId": "next-home", "durationMs": 800 }
{ "buttonId": "talk", "durationMs": 1200 }
```

The first command sends the short-press action for `NEXT`. The second holds the same physical button long enough to send its `HOME` behavior. The third models holding the `TALK` button.

Returns structured input evidence:

```json
{
  "ok": true,
  "requestedAction": "press_button",
  "requestedButtonId": "keyboard-z",
  "inputLayer": "pocketframe-profile",
  "activeLayersBefore": ["fn"],
  "activeLayersAfter": [],
  "resolvedKey": "Left",
  "emittedTransport": "vnc",
  "emittedKey": "Left",
  "emittedKeysym": "0xff51",
  "warnings": [
    "press_button uses PocketFrame device profile projection.",
    "VNC key events are not Linux evdev events."
  ]
}
```

Use this response to avoid confusing device-keycap layers with the target application's true input model. For example, `keyboard-z` plus the Fn layer may project to VNC `Left`; that does not mean the Linux app's evdev contract is `KEY_Z => KEY_LEFT`.

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

### `pocketframe_wait`

Waits for a fixed duration without requiring the framebuffer to change or become stable. Use this for animation delays, app startup pauses, playback buffering, or any case where a scenario needs time to pass even if the screen may not produce a clean synchronization signal.

Arguments:

```json
{
  "durationMs": 1000
}
```

Example result:

```json
{
  "waitedMs": 1003,
  "frameIndex": 123,
  "frameHash": "b4a8..."
}
```

The wait is included in the app-side action trace and can be replayed.

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
