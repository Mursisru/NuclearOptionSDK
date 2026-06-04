# NuclearOptionSDK

IDE and BepInEx bridge for **Nuclear Option** mod development (no-code logic mods, visual HUD, game API explorer, live debugging).

**Current pre-release build:** `0.7.0 Build PR-R2VP1`  
**Target release:** `0.7.0`

> Development is paused for about one week. This pre-release is for early testers and contributors — not a stable “latest” release.

## Components

| Component | Role |
|-----------|------|
| **Nuclear Studio** | Avalonia desktop IDE (Windows, .NET 8) |
| **Nuclear Bridge** | BepInEx plugin in-game (WebSocket `ws://127.0.0.1:9005`) |
| **ModKit / LogicCore** | Logic-mod codegen, MSBuild scaffold, runtime bindings |

## Requirements

- **Nuclear Option** (Steam) with **BepInEx 5 x64**
- **Windows** + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build Studio from source)
- Game install path configured in Studio (Settings) — `Assembly-CSharp.dll` required

## Getting the app (pre-release)

This repository is **source-first**. Pre-built binaries are attached to the GitHub **Pre-release** (not marked as Latest).

### Bridge (in-game)

1. Download `NuclearOptionSDK-Bridge.zip` from the pre-release assets.
2. Extract all DLLs into:
   `Steam\steamapps\common\Nuclear Option\BepInEx\plugins\NuclearOptionSDK\`
3. Restart the game. Check `BepInEx\LogOutput.log` for `Nuclear SDK Bridge` loaded.

### Studio (desktop)

Build locally:

```powershell
git clone https://github.com/Mursisru/NuclearOptionSDK.git
cd NuclearOptionSDK
copy Directory.Build.user.props.example Directory.Build.user.props
# Edit NuclearOptionRoot in Directory.Build.user.props if needed

dotnet build NuclearOptionSDK.slnx -c Release
```

Run:

`src\NuclearOptionSDK.Studio\bin\Release\net8.0-windows\NuclearOptionSDK.Studio.exe`

Or publish a portable folder:

```powershell
dotnet publish src\NuclearOptionSDK.Studio\NuclearOptionSDK.Studio.csproj -c Release -r win-x64 --self-contained false -o publish\Studio
```

## Quick start

1. Start **Nuclear Option** with Bridge installed.
2. Run **Nuclear Studio** → set game path → **Connect** (`ws://127.0.0.1:9005`).
3. **Scene / Game Code / Protocol** tabs — explore API, build logic graphs, **Method Hunter** trace.
4. **Build Logic Mod** exports a BepInEx plugin from `%AppData%\NuclearOptionSDK\logic\project.json`.

## Logs

| App | Path |
|-----|------|
| Studio | `%AppData%\NuclearOptionSDK\logs\studio-*.log` |
| Bridge | `BepInEx\plugins\NuclearOptionSDK_Data\logs\bridge-*.log` |

See `docs/DEV_LOGGING.md`, `docs/API_SURFACE.md`, `docs/DECOMPILER.md`, `docs/PROTOCOL.md`.

## Build (full solution)

```powershell
dotnet build NuclearOptionSDK.slnx -c Release
dotnet test NuclearOptionSDK.slnx -c Release
```

Bridge Release build can auto-deploy to your local game folder when `NuclearOptionRoot` points to the Steam install (see `NuclearOptionSDK.Bridge.csproj`).

## Third-party

ILSpy decompiler engine (MIT), Mono.Cecil — see `ThirdPartyNotices.txt`.

## License

MIT — see `LICENSE`.
