# CLI

`PocketFrame.Cli` is a small human and CI helper. It is not the AI-facing control plane and does not replace MCP.

Runtime commands use the same named pipe automation protocol as MCP, so the GUI app still owns the VNC session, framebuffer, device shell, input mapping, captures, and traces.

## Commands

```bash
pocketframe devices list
pocketframe profiles validate [profile.json|devices-root]
pocketframe scenario validate scenario.json
pocketframe capture screen [output.png]
pocketframe capture device [output.png]
pocketframe trace replay trace.json [delayMs]
```

## Boundaries

- The CLI is for humans and CI.
- The CLI does not expose an HTTP API.
- The CLI does not directly connect to VNC.
- The CLI does not bypass `PocketFrame.App`.
- The CLI does not compete with MCP as the AI automation interface.

## Examples

Validate built-in profiles:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- profiles validate
```

Validate a scenario:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- scenario validate scenarios/cardputer-zero-openbox-smoke.json
```

Capture through the running app:

```bash
dotnet run --project src/PocketFrame.Cli/PocketFrame.Cli.csproj -- capture screen captures/screen.png
```
