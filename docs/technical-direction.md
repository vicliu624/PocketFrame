# Technical Direction

PocketFrame is an MCP-controllable device-shell simulator for pocket-sized Linux computers. Its core product direction is AI-driven debugging of real small-screen Linux sessions inside device-specific shells.

## Current Technical Route

PocketFrame currently uses:

- Avalonia Desktop for the native GUI and device shell rendering.
- A native RFB/VNC client for remote Linux framebuffer display and input forwarding.
- JSON device profiles for shell size, screen position, screen size, and button definitions.
- A shared automation protocol project for internal command and result models.
- An app-side named pipe server for local automation commands.
- An MCP stdio server as the AI-facing automation entry point.

PocketFrame intentionally avoids:

- WebView;
- noVNC;
- Electron;
- browser-based VNC embedding;
- HTTP automation APIs;
- CLI automation APIs;
- VM or hardware emulation in the MVP.

## MCP-Only Automation Model

PocketFrame exposes automation through MCP tools. Internally, the MCP server forwards commands to the running GUI app through a named pipe.

```text
AI Agent
  -> PocketFrame.Mcp over MCP stdio
  -> PocketFrame.App over an internal named pipe
  -> AutomationService
  -> VNC, framebuffer, input, capture, and device models
```

The GUI app starts the internal automation pipe server automatically. The MCP stdio process is normally launched by the MCP host, because stdio MCP requires the client process to own stdin/stdout. PocketFrame ships the MCP server alongside the app, but the GUI does not pre-launch a useful detached MCP stdio session by itself.

## Evolution Plan

### 1. Stabilize Device-Shell Automation

- Expand MCP tool coverage.
- Add action traces and replay files.
- Add frame hash and stable-frame waiting.
- Improve automation error messages.
- Add richer screen and device capture metadata.
- Keep automation coordinates in device screen space, not host desktop space.

### 2. Improve VNC Reliability

- Add CopyRect, Hextile, Tight, and ZRLE encodings.
- Improve framebuffer update scheduling.
- Add connection recovery and reconnect policies.
- Improve pointer motion and keyboard chord fidelity.
- Add optional clipboard support.

### 3. Grow Device Profiles

- Refine Cardputer Zero geometry.
- Refine uConsole geometry.
- Add T-Deck and other cyberdeck-style profiles.
- Add profile validation.
- Support profile-specific keyboard layers and button semantics.
- Support asset-backed shell rendering where vector assets are available.

### 4. Simulate External Device Modules

PocketFrame should eventually simulate external peripherals that small Linux apps commonly depend on. This is still higher-level integration simulation, not low-level bus emulation.

Candidate modules:

- Virtual GPS receiver.
- Virtual LoRa module.
- Virtual serial sensor.
- Virtual battery and power state.
- Virtual network state.
- Virtual button matrix or macro pad.

The likely approach is a module service layer:

```text
ExternalDeviceModule
  -> configured by profile or scenario
  -> controlled by MCP
  -> exposed to the guest through serial, TCP, UDP, files, or a helper daemon
```

Examples:

- A virtual GPS module can stream NMEA sentences over a TCP socket or pseudo-serial bridge.
- A virtual LoRa module can expose a simple serial AT-command style interface.
- A virtual sensor can publish deterministic values for repeatable UI tests.

The goal is to support application-level debugging workflows without claiming to emulate physical buses such as GPIO, I2C, or SPI.

### 5. Scenario and Test Automation

- Add scenario files that describe device profile, VNC target, external modules, and initial state.
- Add deterministic automation runs.
- Add screenshot baselines and frame-diff assertions.
- Add MCP-friendly run summaries.

### 6. Recording and Reporting

- Add screen-only recording.
- Add full-device recording.
- Add ffmpeg integration as an optional backend.
- Generate visual reports with device captures, frame timestamps, and action logs.

## Non-Goals

PocketFrame should not become a full virtual machine manager. It should not manage QEMU, VirtualBox, full hardware emulation, or physical bus emulation in the MVP. Those systems can exist outside PocketFrame and expose a Linux desktop session to it.

PocketFrame should stay focused on:

- device shell context;
- real remote Linux GUI sessions;
- small-screen usability;
- device-aware input;
- AI-controllable automation;
- high-level external module simulation.
