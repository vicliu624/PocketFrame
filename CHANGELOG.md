# Changelog

All notable changes to PocketFrame will be documented in this file.

PocketFrame follows a simple release-log format during early development. The project is pre-1.0, so breaking changes may still occur as the MCP automation model, device profiles, and VNC implementation stabilize.

## [0.1.0] - Initial Release

### Added

- Added the Avalonia desktop application shell.
- Added Cardputer Zero and uConsole device profiles.
- Added rendered device shells with fixed screen viewports.
- Added native RFB/VNC client support for real remote Linux graphical sessions.
- Added Raw framebuffer decoding and nearest-neighbor screen rendering.
- Added VNC keyboard and pointer event forwarding.
- Added Cardputer Zero virtual keyboard layers for blue and orange function keys.
- Added device button and virtual keyboard hit areas.
- Added screen scale support with physical-pixel-aware `1x` behavior.
- Added full-device capture and framebuffer screen capture support.
- Added `PocketFrame.Automation` shared protocol project.
- Added app-side automation named pipe server.
- Added `PocketFrame.Mcp` MCP stdio server.
- Added MCP tools for state, screen capture, device capture, text input, key presses, device buttons, screen clicks, and frame-change waiting.
- Added Automation Inspector for debugging MCP pipe state, frame index, last command, and last result.
- Added English documentation for architecture, device profiles, VNC integration, MCP automation, technical direction, and roadmap.
- Added MIT License.
- Added GitHub Actions workflow for Windows, Linux, and macOS release builds.

### Notes

- This is the first public development release.
- The RFB client is intentionally minimal and currently focuses on MVP correctness over performance.
- PocketFrame does not emulate CPU, GPU, GPIO, I2C, SPI, or full board hardware.
- MCP is the only external automation interface in this release; HTTP and CLI automation APIs are intentionally not included.
