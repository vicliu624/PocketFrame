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
- frame index before and after each action;
- frame hash before and after each action;
- screenshots;
- automation errors.

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
