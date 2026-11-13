# DinoRUN — architecture and runtime flows

This document describes the **current implementation** on the DinoRUN PR #2 branch, not a proposed refactor. Code and Inspector values can differ; verify the scene and prefabs when changing serialized behavior.

## Scope and scene flow

```text
Menu.unity
  MainMenu.StartGame()
       |
       v
GameScene.unity
  MainMenu: wait for input / instructions
       |
       v
GameState.Playing
  PlayerScript + SpikeGenerator + SpeedManager + ScoreManager
       |
       +--- Escape / pause UI ---> GameState.Paused --resume--> Playing
       |
       +--- collision with "spike" tag
                 |
                 v
          GameState.GameOver
            MainMenu: end overlay + score + TryAgain
            DinoRunWebGLBridge: optional result postMessage
```

Enabled build scenes, in order: `Assets/Scenes/Menu.unity`, `Assets/Scenes/GameScene.unity` in `ProjectSettings/EditorBuildSettings.asset`. There is **no separate EndGame scene**.

Before any scene loads, `ManagerBootstrapper.EnsureManagers()` creates `GameStateManager`, `ScoreManager`, `SpeedManager`, `PoolManager` and `DinoRunWebGLBridge` if absent, preserving them with `DontDestroyOnLoad`. In Editor/development builds it also ensures a `DebugHUD` object exists. The HUD is disabled by default.

## Component responsibilities

| Component | Responsibility / dependencies |
| --- | --- |
| `ManagerBootstrapper` | Creates persistent singleton manager GameObjects before first scene; initializes the WebGL bridge. |
| `GameStateManager` | States `Menu`, `Playing`, `Paused`, `GameOver`; emits `OnStateChanged` and owns pause/resume time scale. |
| `ScoreManager` | Stores float score; adds `Time.deltaTime * ScorePerSecond` only while Playing; raises event when the displayed rounded score changes. |
| `SpeedManager` | Computes difficulty progress (score/time), evaluates `speedCurve`, enforces min/max speed and viewport-dependent `GameplaySpeedLimit`. |
| `PlayerScript` | Keyboard/touch/mouse input, buffered jump, grounded contacts, slide hitbox, obstacle death and final-score handoff. |
| `PlayerSlideVisuals` | Adjusts visual pose, animation and short-lived slide particles independently of physics hitbox. |
| `SpikeGenerator` | Alternates/probabilistically chooses obstacles, calculates spacing based on speed, reaction and jump/slide recovery; requests pool spawn. |
| `SpikeScript` | Moves pooled obstacles, handles collisions with the Finish trigger and backup despawn boundary. |
| `PoolManager` | Reuses Rock/Bird obstacle instances; tracks active/available objects and cleans up on scene reload. |
| `GroundTile` | Scrolls and wraps ground tiles while game is Playing. |
| `MainMenu` | Menus, start/wait instructions, score text, in-scene game-over overlay, replay and event subscriptions. |
| `PauseMenuUI` | Creates responsive pause button/modal at runtime; uses state events. |
| `RunnerCameraFraming` | Keeps the player at a sensible horizontal anchor as viewport aspect ratio changes. |
| `DebugHUD` | Optional development diagnostics, normally hidden. |
| `DinoRunWebGLBridge` | Maintains trusted parent origin/received ID, sends result once per page session, bridges Unity and JSLib. |
| `DinoRunWebGLBridge.jslib` | Validates inbound parent messages and posts outbound `unity_game_result` to the configured origin. |

## Gameplay data flow

```text
Input (keyboard / pointer / touch)
   |
   +-- upper half / Up Arrow ----> PlayerScript.RequestJump()
   |                                +-- jump buffer / ground contacts
   |
   +-- lower half / Down Arrow --> PlayerScript.startSlide()
                                    +-- collider height / feet-preserving offset
                                    +-- PlayerSlideVisuals

ScoreManager.Update(): Playing + Time.deltaTime * ScorePerSecond
   | OnScoreChanged when rounded value changes
   +--------------------------> MainMenu score text

ScoreManager / elapsed play time
   +--> SpeedManager: curve, clamp and viewport cap
        +--> SpikeGenerator: safe gap + reaction/recovery time
             +--> PoolManager.Spawn(Bird/Rock)
                  +--> SpikeScript moves left
                       +-- finish/offscreen --> PoolManager.Return()
                       +-- player collision --> PlayerScript.Die()
                                                +--> GameState.GameOver
                                                +--> MainMenu final score
                                                +--> DinoRunWebGLBridge.SendResult()
```

### Timing, collision and pause

- `ScoreManager` uses scaled `Time.deltaTime`; pausing prevents increments and other state-dependent updates.
- `PlayerScript` handles input in `Update` and jump/slide timing in `FixedUpdate`, with a short pre-landing buffer.
- Ground contact tracks supporting colliders so crossing a seam does not incorrectly release the player.
- Sliding shrinks a `CapsuleCollider2D` while preserving its bottom; the collider and artwork are restored when slide ends.
- `SpikeGenerator` uses reaction-time and recovery-distance constraints, in addition to a viewport speed limit.
- `PoolManager` must not queue the same obstacle twice; active obstacles are returned on new single-scene loads.
- `GameStateManager.SetState(Paused)` sets `Time.timeScale=0`, then restores the previous value on resume; returning to the menu while paused should restore time.

## Normal game-over and replay

```text
PlayerScript.OnCollisionEnter2D("spike")
  --> Die() (clears input and movement; sets rigidbody kinematic)
  --> GameStateManager.SetState(GameOver)
  --> MainMenu.HandleStateChanged(GameOver)
       --> ShowTryAgain() + ShowFinalScore(float)
  --> DinoRunWebGLBridge.SendResult((int)ScoreManager.CurrentScore)
       --> only when trusted origin and user_id exist and not already sent
  --> .jslib postMessage({type:"unity_game_result",user_id,score})
```

`MainMenu.TryAgain()` reloads the current scene, while persistent manager objects survive. `MainMenu.OnSceneLoaded` resets score/speed when the game scene is entered. The WebGL bridge **does not reset** its `resultSent` flag on an in-page replay: subsequent runs on the same page will not submit another score until the page is refreshed. This is a known template limitation to address when a buyer defines multi-run behavior.

Visible scores use `F0` rounding in UI, but the WebGL submission truncates the float with a C# `(int)` cast. Do not assume these values are identical for fractional scores.

## Parent iframe message contract

```text
ManagerBootstrapper -> DinoRunWebGLBridge.Start()
  --> DinoRunRequestUserId(ParentOrigin)
       --> window.parent.postMessage("get_user_id", ParentOrigin)

Parent page replies:
  { user_id: "example" } or compatible plain string/number
       --> event.origin === trusted ParentOrigin
       --> event.source === window.parent
       --> SendMessage("DinoRunWebGLBridge","OnUserIdReceived",id)

Eligible normal GameOver:
  --> DinoRunSendPlayerResult(user_id, (int)score, ParentOrigin)
  --> window.parent.postMessage(
        { type:"unity_game_result", user_id, score }, ParentOrigin
      )
```

- `ParentOrigin` is currently an **empty code constant**, not a serialized Inspector setting. Empty disables sending; an exact HTTPS parent origin is mandatory for deployment.
- Incoming response validation lives in `.jslib`, not solely in C#. The JavaScript callback depends on the exact runtime GameObject name.
- The host must restrict replies to the expected iframe origin, validate source, authenticate the associated session and validate score before storage.
- The bridge makes an attempt, without acknowledgement/retry. `SendResult(true)` is not evidence that the backend saved anything.
- This runner does **not** invalidate sessions on browser blur or tab visibility changes. Do not inherit Sorting_Game's different anti-focus policy implicitly.

## Coupling and future modularization candidates

The current template uses Unity singletons, events, scene-name strings and reflection-based regression tests. These are acceptable for an existing small game, but broad extraction into reusable VEREKA modules should be a separate reviewed change. Candidates: state/scoring, host bridge configuration, obstacle progression, and UI contracts.

**Verification:** The owner reported **15/15** Unity regression tests passing, clean compilation and successful manual gameplay for PR #2. The WebGL iframe protocol and backend persistence remain unverified in a real host. Refer to `docs/test-plan.md` for the verification matrix.
