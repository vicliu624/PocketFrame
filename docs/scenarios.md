# Scenarios

Scenarios describe repeatable automation runs. They are intentionally small in the first version and do not replace the GUI, MCP server, VNC client, or device profiles.

The goal is to give future CLI, MCP, report, and CI workflows a shared run description:

- which device profile to select;
- which VNC server to connect to;
- which scale to use;
- where captures and run artifacts should be written;
- what working directory belongs to the run.

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
    "workingDir": "runs/cardputer-zero-openbox"
  }
}
```

The starter scenario is stored at:

```text
scenarios/cardputer-zero-openbox-smoke.json
```

## Boundary

Scenarios are not full test scripts yet. They do not encode actions, assertions, or external modules in this version.

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
  report.md
```
