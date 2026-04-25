# VNC Integration

PocketFrame embeds a native VNC framebuffer into the device screen viewport. It does not use WebView, noVNC, Electron, or browser rendering.

## Test Server on Linux

Install a lightweight VNC environment:

```bash
sudo apt install tigervnc-standalone-server openbox xterm
```

Start a `320x170` VNC session:

```bash
vncserver :10 -geometry 320x170 -depth 24
```

If display `:10` is already running with an older geometry, stop it first and start it again:

```bash
vncserver -kill :10
vncserver :10 -geometry 320x170 -depth 24
```

The running VNC desktop keeps the geometry it was started with. Updating PocketFrame profiles or docs does not resize an existing VNC session.

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
- CopyRect framebuffer encoding
- Hextile framebuffer encoding
- 32-bit true-color framebuffer requests
- None authentication
- VNC password authentication
- Keyboard events
- Pointer events

PocketFrame reports a warning when the remote framebuffer size does not match the selected device profile screen size. This prevents a common automation mistake where clicks and screenshots use the wrong device coordinate space.

Deferred protocol features:

- Tight
- ZRLE
- Cursor pseudo-encoding
- Desktop resize handling

## Input Behavior

Host keyboard input is converted to RFB keysyms by `InputMappingService`. Pointer events inside the screen viewport are sent as RFB pointer events. Clicks on shell virtual buttons are converted to key events instead of pointer events.
