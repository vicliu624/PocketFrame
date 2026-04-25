# CLI

`PocketFrame.Cli` is a small human and CI helper. It is not the AI-facing control plane and does not replace MCP.

Runtime commands use the same named pipe automation protocol as MCP, so the GUI app still owns the VNC session, framebuffer, device shell, input mapping, captures, and traces.

## Commands

```bash
pocketframe devices list
pocketframe connections list
pocketframe profiles validate [profile.json|devices-root]
pocketframe app select-device deviceId
pocketframe app set-scale scale
pocketframe app connect-vnc [--profile id|--host host --port port --password password --device deviceId --scale scale]
pocketframe app disconnect-vnc
pocketframe scenario validate scenario.json
pocketframe scenario run scenario.json [--report|--no-report] [--update-baselines] [--prepare]
pocketframe capture screen [output.png]
pocketframe capture device [output.png]
pocketframe trace show [--limit n]
pocketframe trace save action-trace.json
pocketframe trace clear
pocketframe trace replay trace.json [delayMs]
pocketframe report generate --trace trace.json --out report.md [--scenario scenario.json]
```

## Boundaries

- The CLI is for humans and CI.
- The CLI does not expose an HTTP API.
- The CLI does not bypass `PocketFrame.App`.
- The CLI does not compete with MCP as the AI automation interface.
- App preparation commands still go through the named pipe and let `PocketFrame.App` own VNC.

## Examples

Validate built-in profiles:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- profiles validate
```

Validate a scenario:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- scenario validate scenarios/cardputer-zero-openbox-smoke.json
```

Run a scenario against the currently running app:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- scenario run scenarios/cardputer-zero-openbox-smoke.json --report
```

`scenario run` returns a non-zero exit code when an action fails or an assertion fails.

Prepare the app from the scenario before running:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- scenario run scenarios/cardputer-zero-openbox-smoke.json --prepare --report
```

Connect the running app directly:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- app connect-vnc --host 127.0.0.1 --port 5910 --device cardputer-zero --scale 1
```

Create or approve visual baselines from the current actual screenshots:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- scenario run scenarios/cardputer-zero-openbox-smoke.json --update-baselines
```

Capture through the running app:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- capture screen captures/screen.png
```

Save the current action trace:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- trace save runs/current/action-trace.json
```
