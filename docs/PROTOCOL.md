# IPC Protocol v1

WebSocket `ws://127.0.0.1:9005` (configurable in Bridge).

Envelope:

```json
{ "v": 1, "type": "ping", "id": "...", "payload": { } }
```

Types: `ping`, `pong`, `scene.getRoots`, `scene.tree`, `scene.resolve`, `scene.resolved`, `execute_code`, `execute_result`, `hud.getTree`, `hud.tree`, `hud.update`, `hud.updated`, `overlay.setEnabled`, `overlay.draw`, `audio.event`, `harmony.generate`, `harmony.generated`, `mod.build`, `mod.built`, `error`, `log`.
