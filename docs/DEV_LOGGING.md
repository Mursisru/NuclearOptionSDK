# Dev file logging

Structured logs for automated verification of Studio / Bridge features.

## Studio

Path: `%AppData%\NuclearOptionSDK\logs\studio-YYYY-MM-DD.log`

Format: `timestamp [LEVEL] [action] message`

Logged actions include: `startup`, `connect`, `ipc.send`, `ipc.recv`, `ui`, `visual-hud`.

Shown in Studio **Log** tab (file path at top).

## Bridge

Path: `{NO}\BepInEx\plugins\NuclearOptionSDK_Data\logs\bridge-YYYY-MM-DD.log`

Logged actions include: `system`, `ipc.recv`, `overlay`, `overlay.draw`, `overlay.layout`.

## Agent smoke checklist

1. Start NO + Bridge, open Studio, Connect → `connect` + `ipc.send ping` in studio log; `ipc.recv ping` in bridge log.
2. **Visual HUD Editor** → Add label → Preview → `visual-hud preview` + `overlay.layout` in logs.
3. Scene refresh → `scene.getRoots` in both logs.
4. REPL run → `execute_code` in bridge log.
