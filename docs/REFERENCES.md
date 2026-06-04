# References

## Unity Explorer (patterns only)

| Field | Value |
|-------|--------|
| Local path | `C:\Users\at747\source\repos\UnityExplorer\` |
| Upstream | https://github.com/sinai-dev/UnityExplorer |
| License | **GPL-3.0** |
| Use for NO | BepInEx 5 Mono release (`UnityExplorer.BepInEx5.Mono.zip`) |

NuclearOptionSDK **does not** vendor or copy Unity Explorer source. Read locally for scene tree, reflection UI, and main-thread dispatch patterns.

## Nuclear Option

| Field | Value |
|-------|--------|
| Runtime | Unity **Mono** (not IL2CPP) |
| Managed | `NuclearOption_Data\Managed\Assembly-CSharp.dll` |

## ILSpy decompiler (embedded, MIT)

| Field | Value |
|-------|--------|
| NuGet | `ICSharpCode.Decompiler` |
| Upstream | https://github.com/icsharpcode/ILSpy |
| License | **MIT** |
| In repo | `NuclearOptionSDK.Decompiler` — used by Studio only |

See `docs/DECOMPILER.md`. **dnSpy / dnSpyEx** (GPL-3.0) are not vendored; optional external tool for debug/IL edit.

## dnSpy (reference only)

| Field | Value |
|-------|--------|
| Upstream | https://github.com/dnSpyEx/dnSpy |
| License | **GPL-3.0** |

Read-only patterns; do not copy decompiler source into this SDK.
