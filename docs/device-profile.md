# Device Profiles

Device profiles describe the physical shell and screen geometry used by the simulator. Profiles are JSON files stored under `src/PocketFrame.App/Assets/Devices/<device-id>/profile.json`.

## Schema

```json
{
  "id": "cardputer-zero",
  "name": "Cardputer Zero",
  "backgroundColor": "#121722",
  "screen": {
    "width": 340,
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
      "description": "Confirm"
    }
  ]
}
```

## Coordinates

All coordinates are in unscaled shell pixels. Runtime display scale is applied by the simulator. The screen viewport clips VNC content to `screen.width` and `screen.height`.

## Button Mapping

Each button maps to a symbolic key name such as:

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

## Adding Devices

To add a new device:

1. Create `Assets/Devices/<device-id>/`.
2. Add `profile.json`.
3. Add `shell.svg` or a future shell asset.
4. Restart the app.

The profile service scans all `profile.json` files in the device asset folder.
