**Developer:** Mursisru

# Nuclear Option SDK

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx 5](https://img.shields.io/badge/Bridge-BepInEx%205-orange)](https://docs.bepinex.dev/)
[![Version](https://img.shields.io/badge/Version-0.7.0-green)](https://github.com/Mursisru/NuclearOptionSDK/releases/tag/v0.7.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow)](https://github.com/Mursisru/NuclearOptionSDK/blob/main/LICENSE)

IDE + BepInEx **WebSocket bridge** for **[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/)** mod development. **Nuclear Studio** (Avalonia desktop) connects to an in-game bridge plugin for scene inspection, API exploration, REPL, Visual HUD Editor, and mod scaffolding.

**Bridge GUID:** `com.at747.nuclearoptionsdk.bridge`  
**Release:** `0.7.0` · dev `0.7.0 Build DEV2VP1`

> [!IMPORTANT]
> **Game must be running** with BepInEx and the Bridge plugin loaded before Studio can connect (`ws://127.0.0.1:9005`). **Restart Nuclear Option** after every Bridge rebuild.

---

## Critical warnings

> [!IMPORTANT]
> **BepInEx 5 required for Bridge** - Studio connects to `ws://127.0.0.1:9005` only when Nuclear Option is running with the Bridge plugin loaded.

> [!IMPORTANT]
> **Restart Nuclear Option after every Bridge rebuild** - stale in-game DLL will not match Studio protocol.

> [!WARNING]
> **Do not mix Bridge and Studio versions** - use matched assets from the same release; mismatched protocol fails the WebSocket handshake.

> [!NOTE]
> **Game install path required for Studio** - API index / ILSpy decompilation needs `Assembly-CSharp.dll` at Studio startup.

## Table of contents

- [Critical warnings](#critical-warnings)
- [Quick start (Visual HUD)](#quick-start-visual-hud-no-code)
- [Requirements](#requirements)
- [Install](#install)
- [Projects](#projects)
- [Build](#build)
- [Usage](#usage)
- [Dev logs](#dev-logs)
- [Documentation](#documentation)
- [License](#license)

---

## Quick start (Visual HUD, no code)

1. Start **Nuclear Option** + Bridge, open **Nuclear Studio**, click **Connect**.
2. Tab **Visual HUD Editor** → **+ Label** → drag on canvas → edit text/color.
3. **Preview in game** — labels appear in cockpit overlay.
4. **Save layout** — `%AppData%\NuclearOptionSDK\layouts\visual-hud.json`

---

## Requirements

- **[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/)** (Steam) with **[BepInEx 5](https://docs.bepinex.dev/)**
- **.NET 8 SDK** (Studio) + **.NET Framework 4.8** (Bridge)
- Game install path configured for Cecil / ILSpy index (see `docs/API_SURFACE.md`)
- Windows x64

---

## Install

### Players / mod authors (release)

1. Download **`NuclearOptionSDK-v*-win64.zip`** from [Releases](https://github.com/Mursisru/NuclearOptionSDK/releases).
2. Extract Bridge DLLs to:

   ```text
   Nuclear Option\BepInEx\plugins\NuclearOptionSDK\
   ```

3. Run **`NuclearOptionSDK.Studio.exe`** from the zip (or build output).

> [!WARNING]
> **Do not mix Bridge versions** — Studio and Bridge must come from the same release. Mismatched protocol versions fail the WebSocket handshake.

---

## Projects

| Project | Output |
|---------|--------|
| `NuclearOptionSDK.Protocol` | Shared IPC DTOs |
| `NuclearOptionSDK.Decompiler` | IL→C# via ILSpy engine (MIT), Studio-only |
| `NuclearOptionSDK.Bridge` | `NuclearOptionSDK.Bridge_Engine.dll` + deps → `BepInEx/plugins/NuclearOptionSDK/` |
| `NuclearOptionSDK.Studio` | Avalonia desktop IDE |
| `NuclearOptionSDK.ModKit` | Harmony codegen + MSBuild mod scaffold |

---

## Build

```powershell
cd C:\Users\at747\source\repos\NuclearOptionSDK_Engine
dotnet build NuclearOptionSDK.slnx -c Release
```

Bridge Release copies dependency DLLs to `BepInEx\plugins\NuclearOptionSDK\`. **Restart NO** after rebuild.

---

## Usage

1. Start **Nuclear Option** with BepInEx and Bridge loaded.
2. Run **NuclearOptionSDK.Studio**.
3. Connect to `ws://127.0.0.1:9005`, **Test ping**.
4. Use Scene Explorer, API Explorer (offline Cecil), REPL, HUD Designer, Mod Builder.
5. **Game Code**: ApiSurface index + ILSpy decompilation from `Assembly-CSharp.dll` — see `docs/API_SURFACE.md`, `docs/DECOMPILER.md`. **Game required** at Studio startup.

---

## Dev logs

| Component | Path |
|-----------|------|
| Studio | `%AppData%\NuclearOptionSDK\logs\studio-*.log` |
| Bridge | `BepInEx\plugins\NuclearOptionSDK_Data\logs\bridge-*.log` |

See [docs/DEV_LOGGING.md](docs/DEV_LOGGING.md).

---

## Documentation

| Doc | Topic |
|-----|-------|
| [docs/API_SURFACE.md](docs/API_SURFACE.md) | Cecil API index |
| [docs/DECOMPILER.md](docs/DECOMPILER.md) | ILSpy integration |
| [docs/DEV_LOGGING.md](docs/DEV_LOGGING.md) | Log locations |
| [docs/REFERENCES.md](docs/REFERENCES.md) | Third-party references (UnityExplorer GPL, read-only) |

---

## License

MIT — see [LICENSE](LICENSE).

---

## Keywords

nuclear-option, modding, sdk, bepinex, harmony, avalonia, csharp, unity, ilspy
