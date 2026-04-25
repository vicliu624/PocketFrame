# Architecture

PocketFrame is organized around a strict separation between device presentation, VNC transport, input mapping, capture, and state orchestration.

## Core Boundary

PocketFrame simulates the whole-device usage context, not the hardware platform. It renders a device shell and embeds a real remote Linux graphical session inside a fixed-size screen viewport.

It does not emulate CPU, GPU, GPIO, I2C, SPI, storage, board firmware, or Raspberry Pi hardware behavior.

## Layers

- `Models`: serializable device and simulator state models.
- `ViewModels`: MVVM state orchestration and command entry points.
- `Views`: Avalonia UI controls for toolbar, shell, screen viewport, and framebuffer display.
- `Services`: device profile loading and validation, VNC connection, input mapping, screenshots, and recording placeholder.
- `Vnc`: RFB protocol implementation with no Avalonia dependencies.
- `Automation`: app-side automation service and named pipe server for MCP control.
- `Assets`: device profiles, shell assets, and future previews.

The `PocketFrame.Automation` project contains shared command and response models for the internal pipe protocol. The `PocketFrame.Mcp` project exposes AI-facing MCP tools and forwards commands to the running app through that pipe. The `PocketFrame.Cli` project provides human and CI helper commands and also uses the pipe for runtime actions. MCP remains the AI-facing entry point; automation behavior remains owned by the app-side automation service.

`PocketFrame.DeviceProfiles` contains shared profile models, loading, and validation. `PocketFrame.Scenarios` contains repeatable run definitions. `PocketFrame.Runner` turns scenarios into run artifacts through the automation pipe. `PocketFrame.Reports` contains report models and Markdown writing helpers. These projects consume automation artifacts; they do not own VNC or UI state.

## View Responsibilities

- `MainWindow`: composes the top-level layout and forwards host keyboard events.
- `ToolbarView`: edits device, VNC, scale, screenshot, and recording controls.
- `DeviceShellView`: renders the device shell, screen cutout, and virtual buttons from a profile.
- `ScreenViewport`: clips pointer input and forwards screen pointer events to VNC.
- `VncFramebufferView`: displays the latest VNC framebuffer with nearest-neighbor scaling.

`DeviceShellView` compensates for Avalonia render scaling so `1x` means physical-pixel scale. For example, a `320x170` device screen is intended to occupy `320x170` physical monitor pixels even when the OS desktop scale is above 100%.

Views do not open sockets and do not parse RFB protocol messages.

## Service Responsibilities

- `DeviceProfileService`: loads JSON profiles and provides a Cardputer Zero fallback.
- `VncClientService`: owns the RFB client and exposes framebuffer, status, keyboard, and pointer operations.
- `InputMappingService`: converts Avalonia keys and profile key names to RFB keysyms.
- `ScreenshotService`: captures an Avalonia control to PNG.
- `RecordingService`: preserves the future recording API without affecting the MVP.

## RFB MVP

The initial RFB client supports:

- TCP connection to `host:port`
- RFB 3.3 and 3.8 style negotiation for None or VNC password authentication
- 32-bit BGRA true-color pixel format
- Raw, CopyRect, and Hextile framebuffer updates
- Keyboard and pointer events
- Disconnect and reconnect through service boundaries

More encodings should be added inside `Vnc/` without leaking protocol details into Avalonia views.
