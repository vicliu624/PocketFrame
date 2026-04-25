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
- Document MCP setup and automation coordinate systems.

## Phase 7: External Device Modules

- Add a high-level module system for simulated external devices.
- Prototype a virtual GPS module that can stream deterministic NMEA-like data.
- Prototype a virtual LoRa module with a simple serial or TCP command surface.
- Add virtual serial sensors for repeatable application-level tests.
- Expose module state and controls through MCP tools.
- Keep this layer at the application-integration level, not physical GPIO/I2C/SPI bus emulation.

## Phase 8: AI Debugging Workflows

- Add MCP action traces.
- Add replayable automation sessions.
- Add frame hash and stable-frame waiting.
- Add screenshot baseline comparison.
- Generate debugging reports with screenshots, commands, frame indexes, and module state.

## Future Recording

Recording will remain behind `IRecordingService` and can later be implemented through frame capture, ffmpeg, native platform APIs, VNC-only framebuffer recording, or full shell recording.
