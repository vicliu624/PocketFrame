# Reporting

PocketFrame reports are intended to make AI debugging auditable. A useful run should leave behind evidence, not just a natural-language claim that an action happened.

The planned run layout is:

```text
runs/<timestamp>/
  scenario.json
  trace.json
  screenshots/
    001_before.png
    002_after_type.png
    003_after_click.png
  report.md
```

## Report Contents

A report should record:

- selected device;
- VNC session target;
- each automation action;
- each scenario action result;
- each scenario assertion result;
- frame index before and after each action;
- frame hash before and after each action;
- screenshots;
- automation errors.

Replay evidence should keep original and replay frame hashes separate. A replay result should not overwrite the original action trace entry; it should preserve the original entry and pair it with the replay entry.

## Project

The initial report skeleton lives in:

```text
src/PocketFrame.Reports
```

It currently provides:

- `AutomationRunReport`
- `RunDirectory`
- `MarkdownReportWriter`

The report layer does not run automation by itself. It consumes scenario, trace, state, and screenshot artifacts produced by the automation loop.

Generate a report from an existing trace:

```bash
pocketframe report generate --trace runs/current/action-trace.json --out runs/current/report.md
```

Scenario runs can generate reports automatically:

```bash
pocketframe scenario run scenarios/cardputer-zero-openbox-smoke.json --report
```
