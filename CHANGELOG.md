# Changelog

## 0.7.0 — Pre-release PR-R2VP1 (2026-06-02)

**GitHub:** pre-release only — **not** marked as Latest. Early snapshot before a one-week development pause.

### Highlights

- **Nuclear Studio** (Avalonia): Unity-like workspace, Logic Constructor (reference + user graphs), Visual HUD editor, Game Code (ApiSurface + ILSpy decompiler), Project window.
- **Nuclear Bridge** (BepInEx): WebSocket IPC, scene/HUD/overlay, Roslyn REPL, logic runtime, **Method Hunter** (Harmony trace on game assembly).
- **Logic mods:** MSBuild export (`Build Logic Mod`), typed binding codegen (`BindingCodegen`, `GameCodeIndex`), no mod-specific hardcoding in SDK core.
- **Method Hunter fix (VP70):** trace start/stop on Unity main thread; live trace broadcast every ~0.5s; improved game type enumeration.

### Known limitations

- Field testing of logic mods and Method Hunter in-game is still incomplete.
- Bridge may log Harmony ambiguity on `AudioSource.Play` (audio tracker; does not block Method Hunter).
- UI is **English-first**; ApiSurface phases B–E and full i18n are planned.
- Requires a local Nuclear Option install; Studio does not start without `Assembly-CSharp.dll`.

### Build from this tag

- **Pre-release channel:** `0.7.0 Build PR-R2VP1`
- **Engine dev channel (after sync):** `0.7.0 Build DEV2VP1`

---

## 0.7.0 Build DEV1VP60–VP69 (development notes)

- VP60–68: logic-mod codegen pipeline, `GameCodeIndexBootstrap`, universal Method Hunter, 67 unit tests passing.
- VP69: Studio/Bridge version sync, build warnings cleared, handoff documentation.
