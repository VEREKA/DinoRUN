# Agent instructions — VEREKA DinoRUN

## Boundaries
- Reusable Unity 2D WebGL runner template, **Unity 6000.3.18f1**. Preserve gameplay, scene names, physics, pause semantics, score progression, object pooling and WebGL message format unless expressly asked to change them.
- Work on a **feature branch and PR**. **Never push to or merge into main without explicit owner approval.**
- Keep changes narrow. Never change Unity version, packages, scene YAML, prefab YAML, or existing `.meta` GUIDs casually. Add `.meta` when creating files under `Assets/`.
- Do not commit Unity-generated `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`.

## Gameplay invariants
- Enabled scenes: `Menu` and `GameScene`; GameState: Menu → Playing ↔ Paused → GameOver, then restart/menu.
- Correctly preserve jumping, buffered input, slide hitbox/feet, collisions, obstacle spacing, pooling and score display; read `docs/architecture.md`.
- `ManagerBootstrapper` creates persistent managers; avoid duplicates across scene changes.
- Scene fields override initializers; check saved references, especially prefab and camera assignments.

## WebGL protocol
- `DinoRunWebGLBridge` replaces legacy `Application.ExternalCall("SetUnityScore", ...)`.
- Host origin must be configured as an exact HTTPS origin in `Assets/Scripts/DinoRunWebGLBridge.cs` before deployment. **Never default to a customer-specific address or `*`.**
- Preserve request `get_user_id`, trusted parent `user_id` response and result `{type:"unity_game_result", user_id, score}`. Require valid `event.origin` and `event.source === window.parent`.
- Do not log user identifiers, complete payloads or access tokens.
- A successful `postMessage` is **not** acknowledgement of backend persistence. Host must validate identity/session and stored score; client score is untrusted.
- Do not introduce focus/visibility anti-cheat behavior without explicit owner approval: this template has different game-over semantics from Sorting_Game.

## Validation
- Review README and docs. Run Unity 6000.3.18f1 Test Runner; 15 existing `[UnityTest]` tests transition into Play Mode and exercise real scenes.
- Smoke-test menu, jump, slide, collisions, speed progression, pause, game over/restart, and mouse/touch where applicable.
- For WebGL changes, test iframe handshake, message origin/source filtering, missing ID, single result emission and backend persistence. WebGL testing cannot be simulated by Unity Editor.
- Never claim tests were run unless verified; report test results and exact limitations.
- Read `docs/test-plan.md` before updating any gameplay logic.
