# Scenarios

Scenarios describe repeatable automation runs. They are intentionally small and do not replace the GUI, MCP server, VNC client, target environment adapters, or device profiles.

Scenarios can also describe target environment preparation, including pre-commands, post-commands, and VNC lifecycle.

The goal is to give future CLI, MCP, report, and CI workflows a shared run description:

- which device profile to select;
- which VNC server to connect to;
- which scale to use;
- where captures and run artifacts should be written;
- what working directory belongs to the run.
- which actions should run;
- which assertions decide pass or fail.

## Project

Scenario models live in:

```text
src/PocketFrame.Scenarios
```

The project currently provides:

- `ScenarioDefinition`
- `ScenarioLoader`
- `ScenarioValidator`

## Example

```json
{
  "name": "cardputer-zero-openbox-smoke",
  "deviceId": "cardputer-zero",
  "connection": {
    "host": "127.0.0.1",
    "port": 5910,
    "password": ""
  },
  "scale": 1,
  "captures": {
    "outputDir": "runs/cardputer-zero-openbox"
  },
  "run": {
    "workingDir": "runs/cardputer-zero-openbox",
    "captureOnFailure": true
  }
}
```

The starter scenario is stored at:

```text
scenarios/cardputer-zero-openbox-smoke.json
```

## Boundary

Scenarios are declarative run descriptions. They are not a scripting language and do not encode loops, branching, or arbitrary code.

## Actions

Supported first-version actions:

- `wait`
- `waitFrameChange`
- `waitStableFrame`
- `captureScreen`
- `captureDevice`
- `pressKey`
- `pressButton`
- `clickScreen`
- `typeText`
- `saveTrace`

Example:

```json
{
  "id": "type-ls",
  "type": "typeText",
  "text": "ls\n"
}
```

Button actions can be short presses or holds. Omit `durationMs` or use `0` for a short press. Use a positive value to hold the button, which is how a scenario triggers device-specific long-press behavior such as Cardputer Zero `next-home` sending `Home`.

```json
{
  "id": "hold-next-home",
  "type": "pressButton",
  "buttonId": "next-home",
  "durationMs": 800
}
```

Use `wait` for a fixed time delay. Use `waitFrameChange` when an action should wait for a framebuffer update. Use `waitStableFrame` when the agent needs the screen to settle before observing or capturing.

```json
{
  "id": "wait-player-buffer",
  "type": "wait",
  "durationMs": 2000
}
```

```json
{
  "id": "wait-after-click",
  "type": "waitFrameChange",
  "timeoutMs": 3000
}
```

## Assertions

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

Example:

```json
{
  "id": "after-ls-screenshot-exists",
  "type": "screenshotExists",
  "label": "after-ls"
}
```

Visual baseline assertions compare a captured screenshot label with a PNG baseline. The baseline path is relative to the source scenario file unless it is absolute.

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

The runner writes a diff PNG into the run screenshots directory and records changed pixel count, changed ratio, ignored pixel count, pixel tolerance, and threshold in the report.

`regions` limits comparison to selected rectangles. If omitted, the full image is compared. `ignoreRegions` and `maskRegions` exclude dynamic areas such as clocks, cursors, blinking prompts, or animations. `pixelTolerance` allows small per-channel differences.

Baselines can be created or approved from actual screenshots:

```bash
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --update-baselines
```

## Failure Captures

Run-level failure capture can be enabled with:

```json
{
  "run": {
    "workingDir": "runs/cardputer-zero-openbox",
    "captureOnFailure": true
  }
}
```

Individual actions can override the run default:

```json
{
  "id": "type-ls",
  "type": "typeText",
  "text": "ls\n",
  "captureOnFailure": true
}
```

When an action or assertion fails, the runner captures `failure-screen.png` and `failure-device.png`.

## Running

The runner expects `PocketFrame.App` to already be open. By default it uses the current app device and VNC connection. With `--prepare`, it selects the scenario device, applies the scenario scale, and connects VNC from `connection`.

```bash
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --report
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --prepare --report
```

When `environment.prepare` is true, or when `--prepare` is used, the runner can inspect the target environment, run pre-commands, start or restart VNC, then prepare the app-side device and VNC connection.

The runner creates:

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
