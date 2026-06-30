**Developer:** Mursisru

# NuclearOptionSDK v0.1

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/) [![Nuclear Option SDK](https://img.shields.io/badge/Project-Nuclear%20Option%20SDK-blue)](https://github.com/Mursisru/NuclearOptionSDK) [![Version](https://img.shields.io/badge/Version-0.7.0-green)]() [![License](https://img.shields.io/badge/License-MIT-lightgrey)](LICENSE)


IDE + BepInEx Bridge for **Nuclear Option** mod development. **v0.2** adds **Visual HUD Editor (no-code)** and dev file logging.

## Quick start (Visual HUD, no code)

1. Start NO + Bridge, open Studio, **Connect**.
2. Tab **Visual HUD Editor** → **+ Label** → drag on canvas → edit text/color.
3. **Preview in game** — labels appear in cockpit overlay.
4. **Save layout** — `%AppData%\NuclearOptionSDK\layouts\visual-hud.json`

## Dev logs

- Studio: `%AppData%\NuclearOptionSDK\logs\studio-*.log`
- Bridge: `BepInEx\plugins\NuclearOptionSDK_Data\logs\bridge-*.log`
- See `docs/DEV_LOGGING.md`

## Projects

| Project | Output |
|---------|--------|
| `NuclearOptionSDK.Protocol` | Shared IPC DTOs |
| `NuclearOptionSDK.Decompiler` | IL→C# via ILSpy engine (MIT), Studio-only |
| `NuclearOptionSDK.Bridge` | `NuclearOptionSDK.Bridge_Engine.dll` + deps → `BepInEx/plugins/NuclearOptionSDK/` |
| `NuclearOptionSDK.Studio` | Avalonia desktop IDE |
| `NuclearOptionSDK.ModKit` | Harmony codegen + MSBuild mod scaffold |

## Build

```powershell
cd C:\Users\at747\source\repos\NuclearOptionSDK_Engine
dotnet build NuclearOptionSDK.slnx -c Release
```

Bridge Release build copies **all dependency DLLs** to `BepInEx\plugins\NuclearOptionSDK\` (Fleck, Protocol, Roslyn, …). **Restart NO** after rebuild.

## Usage

1. Start **Nuclear Option** with BepInEx and Bridge plugin loaded.
2. Run **NuclearOptionSDK.Studio**.
3. Connect to `ws://127.0.0.1:9005`, Test ping.
4. Use Scene Explorer, API Explorer (offline Cecil), REPL, HUD Designer, Mod Builder.
5. **Game Code**: ApiSurface (Cecil index + filters + labels) + ILSpy decompilation from `Assembly-CSharp.dll` — see `docs/API_SURFACE.md` and `docs/DECOMPILER.md`. **Game required** at startup.

## References
- Unity Explorer patterns: `source\repos\UnityExplorer\` (GPL, read-only reference)

See `docs/REFERENCES.md`.

---

## Keywords

nuclear-option, modding, sdk, bepinex, harmony, csharp
