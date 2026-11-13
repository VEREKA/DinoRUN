# DinoRUN

A 2D Unity WebGL endless-runner template in which a robot jumps over ground obstacles, slides under flying obstacles and earns points for survival. The game supports keyboard, mouse and touch, increasing difficulty, a pause overlay, and WebGL score submission through a parent website/iframe.

> **Project status:** reusable VEREKA game template, not yet sold or deployed to a customer. Preserve the existing gameplay baseline; configure and test the host integration before any production release.

## Requirements

- **Unity Editor:** `6000.3.18f1` (see `ProjectSettings/ProjectVersion.txt`).
- **Target:** WebGL embedded in an HTTPS host iframe; local Unity Editor play is suitable for gameplay checks, not end-to-end result delivery.
- **Input:** Up Arrow to jump, Down Arrow to slide; mouse/touch on the upper half to jump and lower half to slide. Start and pause controls use Unity UI/EventSystem.
- **Project packages:** restored from `Packages/manifest.json` and `Packages/packages-lock.json`; include Input System, Universal Render Pipeline, TextMesh Pro, uGUI and Unity Test Framework 1.6.0.
- **Scenes:** `Menu` then `GameScene` in `ProjectSettings/EditorBuildSettings.asset`. There is no separate EndGame scene: the game-over UI is part of `GameScene`.

## Open and run

1. Clone the repository, preserving all Unity `.meta` files and folder structure.
2. In Unity Hub, add the directory containing `Assets/`, `Packages/` and `ProjectSettings/`.
3. Open with **Unity 6000.3.18f1**, wait for imports and packages, then open `Assets/Scenes/Menu.unity`.
4. Press Play; use the menu Start button to load `GameScene`. On the game scene, press a key or use touch to begin the run after the initial instructions.
5. For a direct gameplay check, open `Assets/Scenes/GameScene.unity` in the Editor. Persistent managers are created automatically by `ManagerBootstrapper`.
6. To check result delivery, configure a trusted HTTPS parent origin and test an actual WebGL build inside an iframe; Editor play cannot send real `postMessage` results.

The current bridge starts with **no trusted parent origin configured**. This is intentional for a reusable template: gameplay works locally but score submission is disabled until a host is assigned.

## Gameplay

- **Flow:** `Menu` → `GameScene` (waiting for input) → `Playing` ↔ `Paused` → `GameOver`; the game-over UI offers a replay path. The separate `GameStateManager` enum is `Menu`, `Playing`, `Paused`, `GameOver`.
- **Movement:** press **Up Arrow** or click/tap the upper half of the screen to request a jump. A short jump buffer helps inputs just before landing. Press **Down Arrow** or click/tap the lower half while grounded to slide.
- **Obstacles:** `SpikeGenerator` creates ground and flying hazards (Rock and Bird prefabs). The player must jump over one and slide under the other. Hitting an obstacle ends the run.
- **Difficulty:** `SpeedManager` grows speed using an `AnimationCurve`, using score or elapsed play time, with a camera-viewport speed cap and recovery-aware obstacle spacing. `PoolManager` recycles obstacles.
- **Score:** `ScoreManager` accumulates a floating-point score while `Playing`; the UI displays a rounded integer. `PlayerScript.scoreMultiplier` sets the points-per-second rate (currently **4** in code). The WebGL bridge submits an **integer-truncated** value, preserving the old score-cast behavior.
- **Pause:** `GameStateManager` sets `Time.timeScale = 0` and restores it on resume. Touch-friendly `PauseMenuUI` is created at runtime.
- **End of run:** `PlayerScript.Die()` changes state to `GameOver`, the game-over UI displays the result and `DinoRunWebGLBridge` attempts submission only when a trusted origin and host-provided user ID are available.

## Repository layout

```text
Assets/
  Animations/                         Player and bird animations/controllers
  Plugins/
    WebGL/DinoRunWebGLBridge.jslib    iframe postMessage and Unity callback
  Prefabs/
    Bird.prefab
    Rock.prefab
  Resources/fonts/                    UI font assets
  Scenes/
    Menu.unity
    GameScene.unity
  Scripts/
    ManagerBootstrapper.cs           Persistent manager/bridge initialization
    GameStateManager.cs              Menu / Playing / Paused / GameOver
    ScoreManager.cs                  Score accumulation and notifications
    SpeedManager.cs                  Difficulty curve, speed and viewport cap
    SpikeGenerator.cs                Obstacle selection and safe spacing
    SpikeScript.cs                   Obstacle movement and lifecycle
    PoolManager.cs                   Obstacle pooling
    PlayerScript.cs                  Input, jumping, sliding, collision/death
    PlayerSlideVisuals.cs            Slide pose and visual effects
    GroundTile.cs                    Scrolling terrain
    MainMenu.cs                      Menu, in-game HUD and game-over UI
    PauseMenuUI.cs                   Runtime pause overlay
    RunnerCameraFraming.cs           Responsive camera positioning
    DinoRunWebGLBridge.cs            Trusted parent origin / result submission
    DebugHUD.cs                      Development diagnostics
  Tests/Editor/
    DinoRun.RegressionTests.asmdef   Unity test assembly
    GameplayRegressionTests.cs       15 scene-based regression UnityTests
  Settings/                           URP and renderer assets
  Sprites/                            Game and UI sprites
Packages/                              Unity dependencies
ProjectSettings/                       Unity version and build scene list
docs/architecture.md                   Component map and state/data flows
docs/test-plan.md                      Test coverage and release checklist
docs/webgl-integration.md              Host protocol and deployment QA
docs/ci-setup.md                       License-free GitHub Actions checks
scripts/validate_unity_repo.py          Read-only repository integrity validation
scripts/tests/                        Python validator self-tests
.github/workflows/                     PR-triggered Python CI
AGENTS.md                              Coding-agent and contributor rules
README.md                              Project setup and entry point
```

The Unity project also includes the imported TextMesh Pro assets and their `.meta` files. Keep existing GUIDs unchanged when editing project structure.

## WebGL iframe integration

`Assets/Scripts/DinoRunWebGLBridge.cs` and `Assets/Plugins/WebGL/DinoRunWebGLBridge.jslib` implement a message contract compatible with Sorting_Game, but with DinoRUN's own lifecycle.

**Required host configuration:** set `ParentOrigin` in `DinoRunWebGLBridge.cs` to the exact trusted **HTTPS origin** of the parent page, for example `https://games.example.com` (scheme + hostname + optional port; no path, query, trailing slash or wildcard). The repository intentionally leaves it empty. No destination is assumed from another customer's game.

The current exchange is:

1. `ManagerBootstrapper` creates a persistent `DinoRunWebGLBridge` GameObject; the bridge requests a user ID after startup.
2. In WebGL inside an iframe, its JSLib sends `"get_user_id"` to `window.parent` using the configured origin as `targetOrigin`.
3. The host replies with `{ user_id: "..." }` (or a compatible plain string/number). JSLib accepts the reply only if `event.origin` matches the configured parent origin **and** `event.source === window.parent`.
4. The bridge receives the accepted ID through Unity `SendMessage("DinoRunWebGLBridge", "OnUserIdReceived", id)`.
5. On player death, if an ID and trusted origin exist and no result was already attempted in the page session, DinoRUN sends:

```json
{
  "type": "unity_game_result",
  "user_id": "host-provided-user-id",
  "score": 120
}
```

The host must validate identity and score before persisting. **There is no acknowledgement, retry, server-side validation or guaranteed persistence in this repository.** The bridge's `SendResult` result signals local eligibility/attempt, not a saved backend record.

**Host-side sketch (not production authentication):**

```js
window.addEventListener("message", (event) => {
  if (event.origin !== TRUSTED_DINORUN_IFRAME_ORIGIN) return;
  if (event.source !== gameIframe.contentWindow) return;

  if (event.data === "get_user_id") {
    gameIframe.contentWindow.postMessage(
      { user_id: currentUserId },
      TRUSTED_DINORUN_IFRAME_ORIGIN
    );
  } else if (event.data?.type === "unity_game_result") {
    // Validate user/session and score on the backend before storing.
    submitResult(event.data);
  }
});
```

The bridge prevents a second submission attempt **for the same loaded page session**; restarting a run inside the same page does not currently reset that guard. This should be evaluated before a customer deployment where replayed runs must each submit a score. See [WebGL integration contract](docs/webgl-integration.md).

## Build WebGL

1. Install WebGL Build Support for Unity 6000.3.18f1 from Unity Hub, then select WebGL in Unity Build Profiles.
2. Verify that `Menu` and `GameScene` are included in that order.
3. Configure `DinoRunWebGLBridge.ParentOrigin` for the intended trusted host and verify that it matches the **parent**, not the iframe's origin.
4. Build the WebGL player. Serve the generated files from a web server with correct content types and compression handling; opening `index.html` using `file://` is not a reliable test.
5. Embed it in a test page, verify ID request/reply, death submission, rejected origins/sources, and actual backend persistence separately. Follow [the integration checklist](docs/webgl-integration.md) and [regression matrix](docs/test-plan.md).

There is currently **no automated CI WebGL build or deployment**.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| `user_id` never arrives | Is `ParentOrigin` set to an exact HTTPS parent origin? Is the player in an iframe? Does the parent reply to `get_user_id`? Does `event.source` match? |
| Result is not sent on death | A trusted origin, accepted user ID and unsent state are required; see browser console and parent message handler. |
| Second run does not submit | The bridge's duplicate-submission guard persists for the loaded page; a page refresh resets it. Decide whether each replay should be a separate host session before release. |
| Unity Editor shows a bridge mock / disabled warning | Native JSLib communication runs only in a WebGL build; an unset `ParentOrigin` disables messaging. |
| Game appears frozen after pause | Check `GameStateManager.State` and restoration of `Time.timeScale`. |
| Jump/slide or touch fails | Confirm input focus, grounded state, UI interception by `EventSystem` and screen-half mapping. |
| Wrong obstacle behavior | Inspect Bird/Rock prefabs, colliders, `SpikeGenerator` references, spacing, speed and `PoolManager`. |
| UI is outside the screen | Verify Canvas settings, `PauseMenuUI`, camera framing, screen aspect ratio and safe area. |
| Scripts, sprites or scene references are missing | Confirm Unity import and preserved `.meta` GUIDs; inspect Console and serialized references. |

## Development workflow

- Work on a feature branch and submit a PR to `main`; never merge or push directly to `main` without explicit owner approval.
- Read [AGENTS.md](AGENTS.md) before asking Codex or another coding agent to change the repository.
- Read [architecture](docs/architecture.md) for scene lifecycle, state, singleton ownership, pooling, UI and WebGL boundaries.
- Use [the test plan](docs/test-plan.md) to document results per PR and preserve existing gameplay. Avoid unnecessary changes to serialized Unity assets.
- Keep client-specific origins, branding and integrations separate from the reusable VEREKA game template.

## Testing and continuous integration

**Existing Unity tests:** `Assets/Tests/Editor/GameplayRegressionTests.cs` contains **15** `[UnityTest]` regression cases. They enter Play Mode from EditMode and check actual scenes, gameplay, physics, pooling, UI, pause behavior and resolution handling. Open **Window → General → Test Runner → EditMode → Run All** in Unity 6000.3.18f1. The project owner reported **15/15 passing**, clean compilation and a successful manual gameplay smoke test for PR #2. Rerun after code or scene changes.

**GitHub Actions:** [read-only Python repository checks](.github/workflows/repository-checks.yml) run for PRs to `main`, validating Unity metadata GUIDs/companions, project JSON, test-assembly presence and both build scenes. They **do not** start Unity or run Unity's 15 tests. See [CI setup and limitations](docs/ci-setup.md).

**Local Scene Validator:** not yet implemented; planned as a separate PR. Do not treat its absence as evidence that scenes and prefab references are valid.

**WebGL integration:** migrated to the Sorting_Game-compatible protocol, but **not yet tested against a real iframe/backend**; the checked-in host origin is deliberately empty. The 15 Unity tests and manual Editor play do not prove score persistence.

Test status and remaining checks are detailed in [docs/test-plan.md](docs/test-plan.md).
