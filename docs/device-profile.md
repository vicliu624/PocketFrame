# Device Profiles

Device profiles describe the physical shell, screen geometry, optional layered keyboard, and optional shell annotations used by the simulator. Profiles are JSON files stored under `src/PocketFrame.App/Assets/Devices/<device-id>/profile.json`.

Shared profile models, loading, and validation live in `src/PocketFrame.DeviceProfiles`. Both `PocketFrame.App` and `PocketFrame.Cli` use this project so profile validity has one interpretation.

## Schema

```json
{
  "id": "cardputer-zero",
  "name": "Cardputer Zero",
  "backgroundColor": "#121722",
  "screen": {
    "width": 320,
    "height": 170,
    "x": 80,
    "y": 40,
    "scale": 1
  },
  "shell": {
    "width": 480,
    "height": 360,
    "asset": "shell.svg"
  },
  "buttons": [
    {
      "id": "ok",
      "label": "OK",
      "x": 356,
      "y": 262,
      "width": 44,
      "height": 28,
      "key": "Enter",
      "longPressKey": "",
      "description": "Confirm"
    }
  ]
}
```

## Coordinates

All coordinates are in unscaled shell pixels. Runtime display scale is applied by the simulator. The screen viewport clips VNC content to `screen.width` and `screen.height`.

## Button Mapping

Each button maps to a symbolic key name with `key`. A button may also define `longPressKey` when a physical button has separate short-press and hold behavior.

Common symbolic keys include:

- `Up`
- `Down`
- `Left`
- `Right`
- `Enter`
- `Escape`
- `Backspace`
- `F1`
- `F2`
- `Home`

The input mapping service converts these names to RFB keysyms. Device profiles can define different layouts without changing UI code.

Buttons may also define optional layer mappings:

```json
{
  "id": "keyboard-z",
  "label": "Z",
  "x": 120,
  "y": 420,
  "width": 36,
  "height": 24,
  "key": "z",
  "blueKey": "",
  "orangeKey": "Left"
}
```

## Layered Keyboard

Profiles can define `keyboard.layers` and `keyboard.keys` for devices whose physical keyboard has multiple printed legends and layer modifiers.

```json
{
  "keyboard": {
    "layers": ["normal", "fn", "sym", "shift", "ctrl", "alt"],
    "keys": [
      {
        "id": "key-1",
        "label": "1",
        "key": "1",
        "fnKey": "F1",
        "symKey": "!",
        "shiftKey": "!",
        "x": 36,
        "y": 254,
        "width": 54,
        "height": 42,
        "legends": [
          { "text": "F1", "layer": "fn", "color": "#ff5b2e", "position": "topLeft" },
          { "text": "!", "layer": "sym", "color": "#2f6fa5", "position": "topRight" }
        ]
      }
    ]
  }
}
```

Layer keys such as `fn`, `SYM`, and `Aa` use `role: "layer"`. Left click latches the layer for the next key. Right click locks the layer until it is right-clicked again.

## Shell Annotations

Profiles can define `shellAnnotations` for visible hardware markings that do not send input by themselves:

```json
{
  "id": "microsd",
  "kind": "badge",
  "text": "Micro-SD\\nRaspberry Pi OS",
  "x": 228,
  "y": 96,
  "width": 146,
  "height": 58,
  "fill": "#f0f0f0",
  "foreground": "#111111"
}
```

Annotations are suitable for ports, stickers, GPIO labels, status badges, and hardware hints. Add a separate `button` or `keyboard.key` when a visible element must send input.

## Validation

`DeviceProfileValidator` checks profile integrity before profiles are used by the app:

- `id` and `name` must be present.
- Screen and shell dimensions must be positive.
- Screen geometry must stay inside shell bounds.
- `shell.svg` must exist.
- Button IDs must be unique.
- Button geometry must stay inside shell bounds.
- Button keys must be mappable by the input mapping layer.
- Keyboard key IDs must be unique.
- Keyboard key geometry must stay inside shell bounds.
- Keyboard layer outputs must be mappable by the input mapping layer.

Invalid profiles are skipped during startup. If every profile is invalid or missing, the app falls back to the built-in Cardputer Zero profile.

## Adding Devices

To add a new device:

1. Create `Assets/Devices/<device-id>/`.
2. Add `profile.json`.
3. Add `shell.svg` or a future shell asset.
4. Restart the app.

The profile service scans all `profile.json` files in the device asset folder.
