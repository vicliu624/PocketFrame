# Target Environments

PocketFrame target environments describe where the tested Linux desktop and application live. The environment layer is separate from the simulator layer:

- the simulator layer renders the device shell, VNC framebuffer, input mapping, captures, scenarios, and reports;
- the target environment layer prepares Linux-side state such as packages, processes, files, logs, and VNC server lifecycle.

This distinction keeps PocketFrame from becoming a VM manager while still giving AI agents enough structured tools to avoid ad-hoc shell work.

## Profile File

Environment profiles live in:

```text
environments.json
```

Example:

```json
{
  "profiles": [
    {
      "id": "wsl-ubuntu-24.04",
      "name": "WSL Ubuntu 24.04",
      "type": "wsl",
      "distro": "Ubuntu-24.04",
      "workingDirectory": "~",
      "shell": "bash",
      "commandTimeoutMs": 30000,
      "vnc": {
        "display": ":10",
        "host": "127.0.0.1",
        "port": 5910,
        "geometry": "320x170",
        "depth": 24
      }
    }
  ]
}
```

`wsl` is the first adapter because it is the current local development environment. The protocol is not WSL-specific. Additional adapters such as local shell, SSH, device-direct shell, or container targets can reuse the same environment commands.

## Environment Tools

The MCP and CLI expose environment operations through the running `PocketFrame.App` automation pipe so the app can show the AI activity in the lower-left activity panel and include environment actions in the trace.

Supported operations:

- list environment profiles;
- inspect environment and VNC state;
- execute a command with stdout, stderr, exit code, duration, and timeout;
- start, stop, or restart VNC with explicit geometry;
- list processes;
- kill a process by pid or match pattern;
- read and write text files;
- install apt packages;
- launch an app in the background and capture its log path;
- tail a log file.
- inspect, kill, clean, launch, and tail logs for a named target app profile;
- list target Linux evdev input devices and capture a short evdev event window.

Evdev observation is intentionally separate from simulator input injection. PocketFrame can send VNC key events, but VNC-injected keys may not appear on `/dev/input/event*`. Use evdev capture only as target-side evidence when the tested environment provides real input devices or an explicit evdev bridge.

## Scenario Integration

Scenarios can declare an environment:

```json
{
  "environment": {
    "profileId": "wsl-ubuntu-24.04",
    "prepare": true,
    "restartVnc": true,
    "vncDisplay": ":10",
    "vncGeometry": "320x170",
    "vncDepth": 24,
    "preCommands": [
      {
        "id": "install-tools",
        "command": "command -v xterm || sudo apt-get install -y xterm",
        "timeoutMs": 120000
      }
    ],
    "postCommands": [
      {
        "id": "show-processes",
        "command": "ps -eo pid,comm,args | head",
        "continueOnFailure": true
      }
    ]
  }
}
```

When `prepare` is enabled, `scenario run` can prepare the target environment before it prepares the app-side device and VNC connection.

Scenarios can also declare a target app lifecycle harness:

```json
{
  "app": {
    "id": "lofibox",
    "killBeforeLaunch": true,
    "command": "DISPLAY=:10 ./lofibox",
    "workingDirectory": "/home/user/lofibox",
    "processMatch": "lofibox",
    "binaryPath": "/home/user/lofibox/target/debug/lofibox",
    "clearPaths": [".tmp/state", ".tmp/cache"],
    "env": {
      "XDG_STATE_HOME": ".tmp/state",
      "XDG_CACHE_HOME": ".tmp/cache",
      "LOFIBOX_RUNTIME_LOG_PATH": ".tmp/lofibox.log"
    },
    "logPath": ".tmp/lofibox.log"
  }
}
```

The runner executes the harness before framebuffer actions:

1. remove configured state/cache paths;
2. kill the old app process when requested;
3. launch the app command in the target environment;
4. record `app-status.json` with PID, cwd, command line, binary mtime, log path, and state directory.

## Safety Model

Environment automation is intentionally explicit:

- every operation is requested through a structured MCP or CLI command;
- command output, duration, and exit code are returned;
- long-running commands require timeouts;
- environment commands are added to run reports;
- passwords and VNC secrets should not be written into scenario files or command output;
- future adapters can add allowlists or approval policies without changing scenario semantics.
