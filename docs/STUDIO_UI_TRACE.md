# UI interaction trace (только разработка)

Подробный лог мыши, клавиш и split-конструктора. **В релизной публикации отключён** (`STUDIO_UI_TRACE` не определён).

## Когда включён

| Сборка | Как |
|--------|-----|
| **Debug** | Автоматически при запуске Studio |
| **Release + smoke** | `dotnet build -p:StudioUiTrace=true` и `--smoke` |
| **Release publish** | **Нет** (`tools/publish-release.ps1` передаёт `-p:StudioUiTrace=false`) |

## Где лежит лог

- Обычный запуск: `%AppData%\NuclearOptionSDK\logs\ui-trace-YYYYMMDD-HHmmss.log`
- Smoke: `bin\Release\net8.0\smoke-output\ui-trace.log`

В статус-баре Studio (Debug): `UI trace: …`

## Категории

- `pointer.press` / `pointer.move` / `pointer.release` / `pointer.wheel`
- `key.down`
- `split.drag-start` / `split.drag-delta` / `split.drag-end` / `split.pixel` / `split.apply-ratio`
- `split.load` / `split.persist` / `split.unstick`
- `smoke.*` — шаги автотеста

## Перед релизом

1. Publish только через `tools/publish-release.ps1` (без `StudioUiTrace=true`).
2. Убедиться, что в exe нет строк `split.drag-delta` в отладочном смысле — define не задан.
