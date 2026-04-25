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

Connect a VNC session from the PocketFrame GUI. The automation pipe is started automatically when the main window opens. The status bar shows `Automation: MCP pipe ready` when the app-side pipe server is available.

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

Returns the current selected device, VNC status, screen size, shell size, frame index, and last framebuffer update time.

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

## Coordinate System

Automation tools use device-level coordinates:

- `click_screen.x` and `click_screen.y` are VNC framebuffer coordinates.
- Cardputer Zero uses a `340x170` screen coordinate space.
- uConsole uses a `1280x720` screen coordinate space.
- Host window position, OS display scaling, and rendered shell scale are intentionally ignored by automation commands.

## Frame Index

PocketFrame increments `frameIndex` every time a VNC framebuffer update is received. AI agents should use this value for action/observation loops:

1. Call `pocketframe_get_state`.
2. Perform an action such as `pocketframe_type_text` or `pocketframe_click_screen`.
3. Call `pocketframe_wait_frame_change` with the previous frame index.
4. Call `pocketframe_capture_screen`.
5. Inspect the returned screenshot.

## Troubleshooting

- If tools return a pipe connection error, start `PocketFrame.App` first.
- If `capture_screen` fails, connect to a VNC server first.
- Use `Tools -> Automation Inspector` in the GUI to inspect pipe status, frame index, last command, last result, and log path.
- If input logs show key events but the screen does not change, check whether the remote Linux window has focus.
- If no frame change occurs after input, use `pocketframe_capture_screen` to confirm whether the remote desktop is visually idle.
- App-side automation logs are written to `%LOCALAPPDATA%/PocketFrame/input.log` on Windows.
