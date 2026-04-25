# Scenarios

Scenarios describe repeatable automation runs. They are intentionally small and do not replace the GUI, MCP server, VNC client, or device profiles.

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
  "threshold": 0.02
}
```

The runner writes a diff PNG into the run screenshots directory and records changed pixel count, changed ratio, and threshold in the report.

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

The first runner expects `PocketFrame.App` to already be open, connected to VNC, and set to the scenario device.

```bash
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --report
```

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
