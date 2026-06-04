# Game code decompilation (MIT)

Nuclear Studio embeds **ICSharpCode.Decompiler** (ILSpy engine, MIT license) via `NuclearOptionSDK.Decompiler`. We do **not** ship dnSpy or GPL decompiler code in this repository.

## What it does

- Lazy IL → C# decompilation from `NuclearOption_Data/Managed/Assembly-CSharp.dll`
- Disk cache: `%AppData%\NuclearOptionSDK\decompile-cache\`
- Enriches **Game Code** preview and reference-graph drag/drop

See also **`docs/API_SURFACE.md`** — Cecil index, filters, English labels, and REPL translation layer above the decompiler.

## Requirements

1. **Nuclear Option must be installed** — valid install path with `NuclearOption_Data\Managed\Assembly-CSharp.dll`.
2. **No file dump** — `CH\_Nuclear_Option_\Assembly-CSharp` is not used. Type tree = Cecil metadata from DLL; bodies = ILSpy decompiler.

If the game is missing or the path is wrong, Studio shows **GameRequiredWindow** and does not open the IDE (smoke tests use `--smoke` bypass).

Unity managed references (`Managed\*.dll`) are added to the assembly resolver for better type names.

## Policy

1. Methods → always decompile from DLL (cached).
2. Fields / properties → signature from Cecil metadata.

## Type drag (MVP)

Dropping a whole type (e.g. **Aircraft**) builds one reference graph from the **best** method (richest preview or decompiled body), not a multi-method overview.

## dnSpy

Use **dnSpyEx** externally for debugging / IL edit. It is GPL and not embedded in this SDK.
