# DinoRUN — VEREKA

Unity 6 2D endless runner template. The player runs across scrolling ground, jumps over obstacles, slides under flying obstacles and earns score while surviving. The game supports keyboard, mouse and touch input, a pause overlay, a game-over screen and a WebGL iframe integration.

**Status:** reusable template; not yet deployed to a customer. Preserve existing gameplay behavior while adding verification and host integrations via focused PRs.

## Environment

- **Unity:** `6000.3.18f1` (see `ProjectSettings/ProjectVersion.txt`).
- Scenes: `Assets/Scenes/Menu.unity` → `Assets/Scenes/GameScene.unity` (both enabled in `ProjectSettings/EditorBuildSettings.asset`).
- Gameplay scripts: `Assets/Scripts/`.
- Test Framework `1.6.0` with **15 existing** `[UnityTest]` regression cases, in `Assets/Tests/Editor/GameplayRegressionTests.cs`. These tests enter Play Mode and exercise real scenes; the presence of tests does not prove they passed locally.
- There is currently **no GitHub Actions CI** or automated WebGL build.

## Run and test

1. Clone with Unity `.meta` files intact, open the repository folder in Unity Hub with Unity 6000.3.18f1.
2. Open `Assets/Scenes/Menu.unity` or `GameScene.unity`; press Play. Gameplay starts in a waiting state until input starts the run.
3. Verify jump, slide, obstacles, scoring, pause/resume, death and restart in Unity.
4. Run regression tests with **Window → General → Test Runner → EditMode → Run All** (test code uses `EnterPlayMode` inside `[UnityTest]`; do not assume tests are instantaneous).
5. For an actual WebGL integration test, serve a build via HTTP(S) and embed it in a trusted host iframe. See `docs/webgl-integration.md`.

The `DinoRunWebGLBridge` GameObject is bootstrapped at runtime, not stored in a scene. The bridge requires an **explicit trusted parent origin**. An empty origin disables external messaging safely; it is **not** a deployment-ready configuration.

## Project layout

- `Assets/Scripts/ManagerBootstrapper.cs`: persistent managers and WebGL bridge bootstrapping.
- `Assets/Scripts/GameStateManager.cs`: Menu / Playing / Paused / GameOver, including pause time scale.
- `Assets/Scripts/ScoreManager.cs`: score accumulation.
- `Assets/Scripts/SpeedManager.cs`: difficulty curve and speed limit.
- `Assets/Scripts/SpikeGenerator.cs`, `SpikeScript.cs`, `PoolManager.cs`: obstacle spacing, movement and pooling.
- `Assets/Scripts/PlayerScript.cs`, `PlayerSlideVisuals.cs`: input, physics, collision and slide visuals.
- `Assets/Scripts/MainMenu.cs`, `PauseMenuUI.cs`: UI and scene navigation.
- `Assets/Scripts/DinoRunWebGLBridge.cs`, `Assets/Plugins/WebGL/DinoRunWebGLBridge.jslib`: validated iframe handshake and result emission.
- `Assets/Tests/Editor/`: Unity regression tests; test assembly references Unity Test Runner.
- `docs/architecture.md`, `docs/test-plan.md`, `docs/webgl-integration.md`: architecture, validation and host contract.
- `AGENTS.md`: project rules for contributors and coding agents.

## PR workflow

Create a branch and PR for every change. Do not modify the default branch or merge without owner approval. Verify Unity import/test results and manual gameplay when changing scripts. GitHub Actions will be introduced separately; it will not be a substitute for Unity Play Mode or WebGL testing.
