# PocketFrame

PocketFrame is an MCP-controllable device-shell simulator for pocket-sized Linux computers.

PocketFrame lets an AI agent, developer, or tester interact with a real remote Linux graphical session as if it were running inside a specific handheld device shell. It combines:

![PocketFrame UI](docs/ui.png)

- an Avalonia-rendered device body;
- a fixed-size screen viewport;
- a native RFB/VNC framebuffer client;
- device-aware keyboard, pointer, and virtual button mapping;
- screen and device capture;
- an MCP-first automation path for AI-driven debugging.

## Current Development Focus

The current development focus is `0.6.0 - Robust Visual Regression Workflow`. It makes visual regression less brittle with region-limited comparisons, ignored dynamic areas, pixel tolerance, baseline update flows, and richer visual diff reports.

The current direction focuses on:

- stable frame observation through framebuffer hashes and quiet-window waiting;
- replayable automation traces for debugging and regression checks;
- scenario runs that execute actions, evaluate assertions, compare visual baselines, update baselines, and export reports;
- profile validation so bad device geometry fails early;
- a minimal CLI for humans and CI, without competing with MCP.

## Why PocketFrame Exists

Small Linux devices often use unusual screen sizes such as `320x170`, `720x720`, or `1280x720`. A normal desktop window does not show whether an app feels usable on a tiny screen, inside a physical keyboard layout, or behind a device-specific interaction model.

PocketFrame focuses on the whole-device usage context:

- the application is real;
- the Linux session is real;
- the screen size is constrained;
- the shell, buttons, and keyboard layers are device-specific;
- AI agents can observe the framebuffer and perform structured actions through MCP.

The current built-in profiles include:

- Cardputer Zero with a `320x170` screen;
- uConsole with a `1280x720` screen.

## What PocketFrame Is Not

PocketFrame is not a virtual machine and does not emulate hardware. It does not simulate:

- CPU or GPU behavior;
- GPIO, I2C, SPI, or other buses;
- Raspberry Pi / CM hardware;
- storage, firmware, boot, or board-level behavior;
- QEMU, VirtualBox, or container lifecycle management.

PocketFrame expects an existing Linux graphical session exposed over VNC, then places that session inside a device shell and gives humans or AI agents device-level controls.

## Architecture

```text
PocketFrame.App
  - Avalonia GUI
  - Device shell renderer
  - Native RFB/VNC client
  - Input mapping
  - Capture services
  - Automation pipe server

PocketFrame.Mcp
  - MCP stdio server
    -> internal named pipe
    -> PocketFrame.App AutomationService

PocketFrame.Automation
  - Shared command, response, pipe, and result models

PocketFrame.DeviceProfiles
  - Shared device profile models, loader, and validator

PocketFrame.Scenarios
  - Shared scenario definitions for repeatable automation runs

PocketFrame.Runner
  - Scenario runner for auditable run artifacts

PocketFrame.Cli
  - Human and CI helper commands
    -> internal named pipe for runtime actions

PocketFrame.Reports
  - Markdown report models and writers
```

MCP is the AI-facing entry point. The MCP server does not own the VNC session or the UI. It forwards structured commands to the running PocketFrame app through an internal named pipe. PocketFrame intentionally does not expose an HTTP automation API. The CLI is a human/CI helper and uses the same named pipe for runtime actions.

## Why Avalonia + Native VNC

PocketFrame uses Avalonia Desktop for native UI and a native RFB/VNC client for framebuffer display and input forwarding.

The MVP intentionally avoids:

- WebView;
- noVNC;
- Electron;
- browser-embedded VNC;
- web-app architecture.

This keeps the simulator desktop-native and preserves a clean boundary between UI, VNC transport, device profiles, input mapping, capture, and automation.

## Prerequisites

- .NET SDK 9.0 or newer
- A reachable VNC server
- A Linux graphical session sized for the target device

## Run the App

```bash
dotnet restore
dotnet run --project src/PocketFrame.App/PocketFrame.App.csproj
```

The app starts the internal automation pipe server automatically. The status bar shows `Automation: MCP pipe ready` when the pipe is available.

## MCP Startup Model

`PocketFrame.App` automatically starts the internal automation pipe server. The MCP stdio process is normally launched by the MCP host, because stdio MCP requires the client process to own stdin/stdout.

PocketFrame release packages include:

- `PocketFrame.App`, the GUI simulator;
- `PocketFrame.Mcp`, the MCP stdio server;
- `PocketFrame.Cli`, the human and CI helper CLI.

Configure your AI client to launch `PocketFrame.Mcp` after starting `PocketFrame.App`.

## Start a VNC Test Session

On a Linux VM, WSL distro, or device:

```bash
sudo apt install tigervnc-standalone-server openbox xterm
vncserver :10 -geometry 320x170 -depth 24
```

If an old `:10` session is already running with the wrong geometry, restart it:

```bash
vncserver -kill :10
vncserver :10 -geometry 320x170 -depth 24
```

For uConsole:

```bash
vncserver :11 -geometry 1280x720 -depth 24
```

Example `~/.vnc/xstartup`:

```sh
#!/bin/sh
openbox &
xterm
```

PocketFrame connection settings:

- Cardputer Zero: port `5910` for display `:10`
- uConsole: port `5911` for display `:11`
- host: VM, WSL, device, or localhost address
- password: the VNC password, if configured

## Connect from the GUI

1. Launch `PocketFrame.App`.
2. Open `Connection -> Connect...`.
3. Choose or edit a saved connection.
4. Connect to the VNC server.
5. The remote framebuffer appears inside the selected device shell.

## MCP Automation

Start the GUI first, then configure your MCP client to launch:

```bash
dotnet run --project src/PocketFrame.Mcp/PocketFrame.Mcp.csproj
```

Example MCP client configuration:

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

Available MCP tools:

- `pocketframe_get_state`
- `pocketframe_get_profiles`
- `pocketframe_get_connections`
- `pocketframe_select_device`
- `pocketframe_set_scale`
- `pocketframe_connect_vnc`
- `pocketframe_disconnect_vnc`
- `pocketframe_frame_hash`
- `pocketframe_capture_screen`
- `pocketframe_capture_device`
- `pocketframe_type_text`
- `pocketframe_press_key`
- `pocketframe_press_button`
- `pocketframe_click_screen`
- `pocketframe_wait_frame_change`
- `pocketframe_wait_stable_frame`
- `pocketframe_action_trace`
- `pocketframe_replay_log`

See `docs/mcp-automation.md` for schemas, examples, coordinate rules, and troubleshooting.

## Scenarios

PocketFrame includes a first scenario definition layer in `src/PocketFrame.Scenarios`.

Starter scenario:

```text
scenarios/cardputer-zero-openbox-smoke.json
```

Scenarios describe device selection, VNC connection settings, display scale, capture output directory, run working directory, actions, visual baselines, failure captures, and assertions.

Scenario files are declarative run descriptions. They are not a scripting language.

## CLI

The CLI is intentionally small and does not replace MCP. Runtime commands still talk to the running GUI app through the automation pipe.

```bash
pocketframe devices list
pocketframe connections list
pocketframe profiles validate
pocketframe app select-device cardputer-zero
pocketframe app set-scale 1
pocketframe app connect-vnc --host 127.0.0.1 --port 5910 --device cardputer-zero --scale 1
pocketframe app disconnect-vnc
pocketframe scenario validate scenarios/cardputer-zero-openbox-smoke.json
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --report
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --prepare --report
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --update-baselines
pocketframe capture screen captures/screen.png
pocketframe capture device captures/device.png
pocketframe trace show --limit 20
pocketframe trace save runs/current/action-trace.json
pocketframe trace clear
pocketframe trace replay runs/cardputer-zero-openbox/action-trace.json
pocketframe report generate --trace runs/current/action-trace.json --out runs/current/report.md
```

The CLI is for humans and CI. AI automation should continue to use MCP.

Runtime CLI commands do not bypass `PocketFrame.App`; capture and trace replay commands use the same named pipe automation protocol as MCP.

See `docs/cli.md`.

## Scenario Runner

`PocketFrame.Runner` turns a validated scenario into an auditable run directory. By default it uses the currently running app state. With `--prepare`, it selects the scenario device, sets the scenario scale, and connects VNC from the scenario connection settings before running.

```bash
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --report
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --prepare --report
```

Output layout:

```text
runs/<scenario-name>/<timestamp>/
  scenario.json
  state.json
  action-trace.json
  screenshots/
    initial-screen.png
    initial-device.png
    failure-screen.png
    failure-device.png
    <assertion-id>-diff.png
  report.md
```

Supported first-version actions:

- `waitStableFrame`
- `captureScreen`
- `captureDevice`
- `pressKey`
- `pressButton`
- `clickScreen`
- `typeText`
- `saveTrace`

Supported first-version assertions:

- `frameChanged`
- `screenshotExists`
- `frameHashNotEmpty`
- `frameHashEquals`
- `frameHashNotEquals`
- `actionSucceeded`
- `actionFailed`
- `allActionsSucceeded`
- `screenshotMatchesBaseline`

Visual baseline assertions compare an actual screenshot label with a PNG baseline. Optional regions, ignored regions, and pixel tolerance make the comparison more robust against dynamic UI areas and small rendering differences:

```json
{
  "id": "after-ls-matches-baseline",
  "type": "screenshotMatchesBaseline",
  "label": "after-ls",
  "baseline": "baselines/cardputer-zero-openbox/after-ls.png",
  "threshold": 0.02,
  "pixelTolerance": 8,
  "regions": [
    { "x": 0, "y": 0, "width": 320, "height": 150 }
  ],
  "ignoreRegions": [
    { "x": 300, "y": 0, "width": 40, "height": 20 }
  ],
  "maskRegions": [
    { "x": 0, "y": 160, "width": 320, "height": 10 }
  ]
}
```

Use `--update-baselines` to create or approve baselines from the current actual screenshots.

## Automation Loop

The recommended AI loop is:

1. Send an action through MCP, such as text input, a key press, a device button press, or a screen click.
2. Wait with `pocketframe_wait_stable_frame`.
3. Read `pocketframe_frame_hash` or capture the screen.
4. Inspect the stable result.
5. Save `pocketframe_action_trace` when the run should be replayed or audited.

`pocketframe_wait_frame_change` remains available, but `pocketframe_wait_stable_frame` is the better default for reasoning because it waits for the screen to settle, not merely change once.

## Automation Inspector

Open:

```text
Tools -> Automation Inspector
```

The inspector shows:

- automation pipe status;
- pipe name;
- selected device;
- VNC status;
- frame index;
- last framebuffer update time;
- last automation command;
- last result or error;
- log file location;
- the MCP server launch command.

It is a human debugging panel for the MCP bridge. It does not execute tools.

The main window also shows a compact MCP activity panel in the lower-left corner of the simulator area. It lists the latest AI requests and simulator responses, such as typed text, key presses, screen clicks, captures, VNC connection preparation, and result summaries, without exposing VNC passwords.

## Coordinate System

Automation commands use device-level coordinates:

- `pocketframe_click_screen` uses VNC framebuffer coordinates.
- Cardputer Zero screen coordinates are `320x170`.
- uConsole screen coordinates are `1280x720`.
- Host window position and OS display scaling are ignored by automation tools.

The GUI still supports visual scaling for humans, but automation operates on stable device coordinates.

## Pixel Scale

PocketFrame treats `1x` as physical-pixel scale. On desktops configured to 125%, 150%, or 200% display scaling, the simulator compensates for the OS render scale so a Cardputer Zero `320x170` screen occupies approximately `320x170` physical monitor pixels.

## Device Profiles

Device profiles live under:

```text
src/PocketFrame.App/Assets/Devices/<device-id>/
```

A profile folder contains:

- `profile.json`
- `shell.svg`
- optional `preview.png`

The profile defines:

- shell size;
- screen size;
- screen position;
- button geometry;
- key mapping.

See `docs/device-profile.md`.

## Technical Direction

PocketFrame is evolving toward MCP-first AI automation, richer frame observation, and high-level external device module simulation. Planned module examples include virtual GPS, virtual LoRa, virtual serial sensors, battery state, and network state.

See `docs/technical-direction.md` for the detailed technical route and evolution plan.

## Changelog

See `CHANGELOG.md` for release notes.

## Current Limits

- The RFB client is still intentionally minimal.
- Raw, CopyRect, and Hextile encodings are supported; Tight and ZRLE are deferred.
- MCP automation controls the running GUI app through a local named pipe.
- The CLI is a human/CI helper and is not the AI-facing control plane.
- The MCP server expects `PocketFrame.App` to already be running.
- `scenario run` can prepare the device, scale, and VNC connection with `--prepare`, but it still expects `PocketFrame.App` to already be running.
- `capture_screen` is framebuffer-based; `capture_device` captures the rendered Avalonia device view.
- Reports are generated from scenario, state, actions, assertions, visual baseline diffs, trace, and screenshot artifacts.
- Recording remains a placeholder service.
- Device shells are still approximate and can be refined.

## Roadmap

- Improve RFB performance and add more encodings.
- Add Tight and ZRLE VNC encoding support.
- Improve screenshot baseline comparison with mask images and region-level reports.
- Add high-level external module simulation such as virtual GPS and virtual LoRa.
- Add more device profiles such as T-Deck and additional cyberdeck layouts.
- Refine shell artwork and keyboard legends.
- Implement recording through frame capture or ffmpeg.
