# Roadmap

## Phase 1: Avalonia Shell Prototype

- Create the Avalonia desktop project.
- Render a Cardputer Zero-style shell.
- Reserve a `340x170` screen viewport.
- Support `1x`, `2x`, `3x`, and `4x` display scaling.
- Show virtual button hit areas.

## Phase 2: VNC Framebuffer Display

- Add VNC host, port, and password settings.
- Connect to a VNC server.
- Render Raw framebuffer updates inside the screen viewport.
- Add CopyRect and Hextile encoding support.
- Add clearer connection timeout, authentication failure, disconnect, reconnect, and framebuffer-size mismatch status.
- Support disconnect and reconnect.

## Phase 3: Input Mapping

- Forward host keyboard input to the VNC session.
- Forward screen pointer input as VNC pointer events.
- Convert virtual shell button clicks to VNC key events.

## Phase 4: Capture

- Implement full device PNG capture.
- Add screen-only capture.
- Add user-configurable capture directory.

## Phase 5: Device Configuration

- Load Cardputer Zero from JSON.
- Add profile validation.
- Refine the uConsole profile and add T-Deck starter profiles.
- Use shell assets for more precise visual rendering.

## Phase 6: MCP Automation

- Add the `PocketFrame.Automation` shared protocol project.
- Start an app-side automation named pipe server.
- Add the `PocketFrame.Mcp` stdio server.
- Expose MCP tools for state, screen capture, text input, key presses, device buttons, screen clicks, and frame-change waiting.
- Add frame hashing, stable-frame waiting, action traces, replay logs, and stable automation error codes.
- Document MCP setup and automation coordinate systems.

## Phase 7: Scenarios and Validation

- Add `PocketFrame.Scenarios` as the shared scenario definition project.
- Define scenario files for device selection, VNC connection, scale, capture output, and working directories.
- Add profile validation for required fields, geometry, assets, button uniqueness, and mappable keys.
- Prepare scenario data for future CLI, MCP, report, and CI workflows.

## Phase 8: CLI and Reports

- Add a minimal CLI for humans and CI.
- Keep runtime CLI commands on the named pipe automation protocol.
- Add commands for device listing, profile validation, scenario validation, captures, and trace replay.
- Add report models and Markdown output for scenario, trace, screenshot, frame index, frame hash, and error evidence.

## Phase 9: Scenario Runner and Tests

- Add `PocketFrame.DeviceProfiles` to remove duplicate profile semantics.
- Add `PocketFrame.Runner` for scenario-based run directories.
- Add CLI trace show/save/clear and report generation commands.
- Add focused tests for validators, reports, automation serialization, and framebuffer operations.

## Phase 10: External Device Modules

- Add a high-level module system for simulated external devices.
- Prototype a virtual GPS module that can stream deterministic NMEA-like data.
- Prototype a virtual LoRa module with a simple serial or TCP command surface.
- Add virtual serial sensors for repeatable application-level tests.
- Expose module state and controls through MCP tools.
- Keep this layer at the application-integration level, not physical GPIO/I2C/SPI bus emulation.

## Phase 11: AI Debugging Workflows

- Add declarative scenario actions.
- Add basic scenario assertions.
- Add assertion-based report summaries.
- Add screenshot baseline comparison.
- Generate debugging reports with screenshots, commands, frame indexes, and module state.

## Future Recording

Recording will remain behind `IRecordingService` and can later be implemented through frame capture, ffmpeg, native platform APIs, VNC-only framebuffer recording, or full shell recording.
