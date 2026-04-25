# VNC Integration

PocketFrame embeds a native VNC framebuffer into the device screen viewport. It does not use WebView, noVNC, Electron, or browser rendering.

## Test Server on Linux

Install a lightweight VNC environment:

```bash
sudo apt install tigervnc-standalone-server openbox xterm
```

Start a `340x170` VNC session:

```bash
vncserver :10 -geometry 340x170 -depth 24
```

Start a `1280x720` uConsole-style session:

```bash
vncserver :11 -geometry 1280x720 -depth 24
```

Connect from PocketFrame:

- Host: `VM_IP`
- Port: `5910`
- Password: the VNC password configured by `vncpasswd`

For the uConsole session, use port `5911`.

## Startup Session

You can place this in `~/.vnc/xstartup`:

```sh
#!/bin/sh
openbox &
xterm
```

Make it executable:

```bash
chmod +x ~/.vnc/xstartup
```

## RFB Support

The MVP RFB client supports:

- Raw framebuffer encoding
- 32-bit true-color framebuffer requests
- None authentication
- VNC password authentication
- Keyboard events
- Pointer events

Deferred protocol features:

- CopyRect
- Hextile
- Tight
- Cursor pseudo-encoding
- Desktop resize handling

## Input Behavior

Host keyboard input is converted to RFB keysyms by `InputMappingService`. Pointer events inside the screen viewport are sent as RFB pointer events. Clicks on shell virtual buttons are converted to key events instead of pointer events.
