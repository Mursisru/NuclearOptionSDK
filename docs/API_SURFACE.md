# ApiSurface — game API abstraction layer

ApiSurface sits between **Mono.Cecil** / **ILSpy decompiler** and Studio UI (Game Code tree, drag-and-drop, REPL). It replaces ad-hoc string indexing and dump-era catalogs with a structured symbol model.

## Location

All code lives in `NuclearOptionSDK.Studio/Services/ApiSurface/` (not a separate NuGet project).

## Layers

1. **Filtering** — hide compiler noise, optional Unity lifecycle methods, accessor pairs (`get_`/`set_`).
2. **Labeling** — `ILabelResolver` chain: `locales/en/api-symbols.json` → humanize → technical fallback.
3. **Taxonomy** — keyword categories, `[AVIATION]`-style type tags, optional category grouping in Game Code.
4. **Collisions** — `ApiSymbolId` (type + kind + member + optional composition path); badges for inherited/composed members.
5. **REPL** — `ReplSurfaceTranslator` maps friendly aliases (`current_aircraft`) to technical C# before Bridge Roslyn.

## Symbol keys

```text
Member.{TypeFullName}.{memberName}
Read.{ShortType}.{memberName}   // when present in logic catalog
```

Factory: `ApiSymbolIdFactory`.

## Configuration

| File | Purpose |
|------|---------|
| `Defaults/api-surface-rules.json` | Filter defaults (copied to output) |
| `%AppData%\NuclearOptionSDK\api-surface\rules.json` | User override |
| `Defaults/locales/en/api-symbols.json` | Curated English titles/hints |
| `Defaults/repl-aliases.json` | REPL friendly → technical aliases |

## Game Code UI

- **Friendly labels** — `SymbolLabelService` (replaces `PlainLabelService` for game tree).
- **Hide system noise** / **Show Unity lifecycle** — reload index from DLL.
- **Group members by category** — taxonomy view per type.
- **Behavior folders** under each type (no line prefixes):
  - `Actions` — methods/constructors; signatures include `()` and return type.
  - `Data` — scalars (int, float, string, bool).
  - `Reference` — fields/properties pointing at game types (`Player`, `Aircraft`, …).
- **Context engine** — `MemberContextAnalyzer` cross-checks **owner type** (e.g. `GameplayUI` → UI) and **CLR type** (e.g. `Player` → object reference) to build English hints and inspector lines.

## Logic catalog

`NoGameParameterCatalog` remains for **logic palette** curated entries (Telemetry, Gates, Actions). Dump-generated `Data.cs` is excluded from build; game members are discovered via Cecil only.

## Tests

`tests/NuclearOptionSDK.Studio.Tests/ApiSurfaceTests.cs` — set `NO_GAME_ROOT` for integration tests against a real install.
