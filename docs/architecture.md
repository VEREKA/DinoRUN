# DinoRUN — architecture and runtime flow

This describes the current implementation in Unity 6000.3.18f1.

## Scenes and lifecycle

`Menu.unity` → `GameScene.unity`. `ManagerBootstrapper` creates `GameStateManager`, `ScoreManager`, `SpeedManager`, `PoolManager` and `DinoRunWebGLBridge` as persistent objects before the first scene loads. Scene controllers reference these singletons.

The state enum is `Menu`, `Playing`, `Paused`, `GameOver`. Pausing sets `Time.timeScale=0`, freezing time-dependent physics, score and slide, and restores it when resuming or leaving pause.

`MainMenu` manages starting, waiting for input, end-of-run UI, score text and scene reloads. `PauseMenuUI` is constructed dynamically at runtime and supports pointer input.

## Gameplay flow

1. `PlayerScript` reads keyboard and touch/mouse input. Touch/click on the upper half requests jump; lower half requests slide. Jump buffer preserves a short pre-landing input.
2. `ScoreManager` accumulates floating-point score while `GameState.Playing`; the visible score is rounded and the score change event fires when the displayed integer changes. `PlayerScript.scoreMultiplier` configures points per second.
3. `SpeedManager` uses an animation curve to accelerate over score or time, capped by a viewport-dependent `GameplaySpeedLimit`.
4. `SpikeGenerator` selects bird/rock obstacles and spacing with recovery/reaction time constraints. `PoolManager` reuses `SpikeScript` instances; scrolling `GroundTile` components move independently.
5. On collision with a tagged obstacle, `PlayerScript.Die()` switches to `GameOver`, updates score UI via existing state observers and attempts one WebGL bridge result submission.

Avoid silently changing numeric gameplay values: score-per-second, jump force, slide duration, gap generation and speed limits need focused tests or manual validation.

## WebGL host contract

`DinoRunWebGLBridge` is created by `ManagerBootstrapper`, persists across scenes, and requests a user ID at startup. It will only communicate with a parent iframe when `ParentOrigin` is set to an exact HTTPS origin; default empty means no outbound messages.

`Assets/Plugins/WebGL/DinoRunWebGLBridge.jslib` accepts IDs only from `window.parent` with the configured `event.origin`, then calls `SendMessage("DinoRunWebGLBridge", "OnUserIdReceived", userId)`.

On death, `PlayerScript` sends the **integer-truncated score** through the bridge. The JavaScript parent receives `{type:"unity_game_result",user_id,score}`. The bridge suppresses duplicate submissions during a single app session; missing ID/origin prevents sending. There is no delivery acknowledgement or retry.

**Important:** This template does not invalidate a run when browser focus changes. Do not port Sorting_Game's visibility callbacks unless explicitly requested. Full host integration requires a test page and backend validation; see `docs/webgl-integration.md`.
