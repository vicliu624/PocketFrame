# Changelog

All notable changes to PocketFrame will be documented in this file.

PocketFrame follows a simple release-log format during early development. The project is pre-1.0, so breaking changes may still occur as the MCP automation model, device profiles, and VNC implementation stabilize.

## [Unreleased]

## [0.9.1] - 2026-04-26

### Fixed

- Fixed an automation activity crash when nullable numeric JSON fields such as `pid: null` were displayed for environment process commands.

## [0.9.0] - 2026-04-26

### Added

- Added structured input truth echo for `press_key` and `press_button`, including active layers, resolved keys, emitted VNC transport/key/keysyms, and warnings that profile projections and VNC keys are not Linux evdev truth.
- Added input action results to automation traces and Markdown report input audit sections.
- Added MCP activity summaries for structured input results, app lifecycle operations, and evdev capture.
- Added scenario `app` lifecycle configuration for target app status, stale process killing, state/cache cleanup, background launch, log paths, binary metadata, and optional cleanup.
- Added MCP target app lifecycle tools for status, kill, launch, clean state, and tail log operations.
- Added MCP evdev observation tools for listing `/dev/input/event*` devices and capturing short evdev event windows with explicit VNC caveats.
- Added `logContains` and `jsonEquals` scenario assertions for target-side semantic evidence from logs and JSON snapshots.
- Added tests for input echo serialization, app lifecycle scenario validation, semantic assertions, app harness runner flow, and report input audit output.

### Changed

- Improved VNC framebuffer rendering throughput by avoiding per-frame framebuffer cloning, reusing the Avalonia bitmap, coalescing queued UI frame updates, and disabling TCP Nagle delays for the VNC socket.
- Updated MCP tool descriptions to distinguish PocketFrame profile projection, VNC logical key emission, and Linux evdev observation.

## [0.8.0] - 2026-04-26

### Added

- Added layered keyboard profile models for physical keys, key legends, key layers, and shell annotations.
- Added an updated Cardputer Zero hardware profile with the revised keyboard layout, Fn/SYM/Aa layers, USB/GPIO/Micro-SD/Boot/LAN/Power visual annotations, and AI-readable input metadata.
- Added MCP tools for `pocketframe_get_input_model` and `pocketframe_get_keyboard_state`.
- Added `PocketFrame.Environments` as a target environment automation layer separate from the simulator layer.
- Added `environments.json` with a WSL Ubuntu 24.04 profile for local Linux desktop and VNC preparation.
- Added environment MCP tools for profile listing, state inspection, command execution, VNC lifecycle, process listing, process killing, file read/write, apt package installation, background app launch, and log tailing.
- Added matching CLI environment commands for human and CI use through the same app automation pipe.
- Added scenario `environment` configuration with pre-commands, post-commands, and VNC start/restart preparation.
- Added environment command evidence to scenario run results and Markdown reports.
- Added environment validation and parsing tests.
- Added hold-duration support to MCP and scenario `pressButton` automation so device long-press actions can be driven by AI agents.
- Added input-model metadata for button short-press keys, long-press keys, and long-press availability.
- Added plain time-based `wait` automation for MCP, CLI, scenario actions, action traces, and replay logs.
- Added scenario `waitFrameChange` actions to align scenario synchronization with MCP synchronization.

### Changed

- Cardputer Zero keyboard rendering now uses profile-driven key legends instead of hardcoded row text.
- `scenario run --prepare` can now prepare the target Linux environment before selecting the device, setting scale, and connecting VNC.
- MCP activity text now distinguishes short button presses from held button actions.

## [0.7.0] - 2026-04-25

### Added

- Added a compact lower-left MCP activity panel in the main window.
- Added app-side automation activity events so the UI can show recent MCP commands and results.
- Expanded the MCP activity panel to show readable AI requests and simulator responses while hiding sensitive VNC password values.
- Added automation methods for listing profiles and connections, selecting devices, setting scale, connecting VNC, and disconnecting VNC.
- Added MCP tools for environment preparation before AI interaction.
- Added CLI helpers for app environment preparation.
- Added `scenario run --prepare` to select the scenario device, set scale, and connect VNC before running.

### Changed

- Corrected the Cardputer Zero screen specification from `340x170` to `320x170`.

## [0.6.0] - 2026-04-25

### Added

- Added region-limited visual baseline comparisons.
- Added `ignoreRegions` and `maskRegions` for dynamic or noisy visual areas.
- Added `pixelTolerance` for small per-channel rendering differences.
- Added `scenario run --update-baselines` for creating or approving screenshot baselines from actual captures.
- Added expanded visual diff sections in Markdown reports.
- Added direct `VisualBaselineComparer` tests for identical images, thresholds, regions, size mismatch, and RGB/grayscale PNG decoding.

### Changed

- Visual baseline reports now include ignored pixel count and pixel tolerance evidence.

## [0.5.0] - 2026-04-25

### Added

- Added `screenshotMatchesBaseline` scenario assertions with actual, baseline, diff, changed pixel count, changed ratio, and threshold evidence.
- Added `frameHashEquals`, `frameHashNotEquals`, `actionSucceeded`, `actionFailed`, and `allActionsSucceeded` assertions.
- Added run-level and action-level `captureOnFailure` support for automatic failure screenshots.
- Added a dependency-free PNG baseline comparer for small visual regression checks.
- Added runner tests for assertion failure, failure captures, and screenshot baseline comparison.

### Changed

- Updated reports to include visual baseline evidence columns.
- Updated the starter smoke scenario to capture failure evidence and assert that all actions succeeded.

## [0.4.0] - 2026-04-25

### Added

- Added scenario `actions` for `waitStableFrame`, `captureScreen`, `captureDevice`, `pressKey`, `pressButton`, `clickScreen`, `typeText`, and `saveTrace`.
- Added scenario `assertions` for `frameChanged`, `screenshotExists`, and `frameHashNotEmpty`.
- Added scenario action and assertion results to `ScenarioRunner`.
- Added report summary, action result table, and assertion result table.
- Added `IAutomationClient` and `PipeAutomationClient` so runner tests can use fake automation clients.
- Added `PocketFrame.Runner.Tests` for runner success, not-connected, and device-mismatch paths.

### Changed

- Updated the starter smoke scenario to execute actions and assertions.
- Updated `scenario run` to return assertion-based success or failure.

## [0.3.0] - 2026-04-25

### Added

- Added `PocketFrame.DeviceProfiles` with shared device profile models, loading, and validation.
- Added `PocketFrame.Runner` with a first scenario runner that creates auditable run directories.
- Added `pocketframe scenario run` for scenario-based state capture, initial screenshots, trace export, and report generation.
- Added `pocketframe trace show`, `pocketframe trace save`, and `pocketframe trace clear`.
- Added `pocketframe report generate`.
- Added focused test projects for automation serialization, scenarios, reports, device profiles, and framebuffer operations.

### Changed

- Reused shared device profile validation from both the app and CLI to avoid duplicate profile semantics.
- Updated CI to run tests before publishing release artifacts.

## [0.2.0] - 2026-04-25

### Release Focus

This release is a correction-and-regression release. It stabilizes PocketFrame around the AI automation loop, keeps MCP as the AI-facing interface, adds a small CLI for humans and CI, and improves VNC behavior before expanding into external device simulation.

### Added

- Added framebuffer hashing for automation observations.
- Added stable-frame waiting for reliable AI action/observation loops.
- Added app-side automation action traces.
- Added replay support for saved automation action traces.
- Added stable automation error code constants.
- Added `PocketFrame.Scenarios` for repeatable run definitions.
- Added a starter Cardputer Zero Openbox smoke scenario.
- Added device profile validation for required fields, geometry, assets, button uniqueness, and mappable keys.
- Added CopyRect and Hextile VNC framebuffer decoding.
- Added `PocketFrame.Cli` with minimal human and CI commands.
- Added `PocketFrame.Reports` with run report models, run directory creation, and Markdown report writing.
- Added CLI documentation.
- Added scenario and reporting documentation.

### Changed

- Clarified that MCP remains the AI-facing automation path.
- Clarified that CLI commands are for humans and CI and use the same named pipe automation protocol for runtime actions.
- Improved VNC connection timeout, authentication failure, disconnect, reconnect, and framebuffer-size mismatch status paths.
- Updated the release workflow to publish `PocketFrame.Cli`.

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
