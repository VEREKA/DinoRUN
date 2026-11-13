# Agent instructions — VEREKA DinoRUN

These instructions apply to AI coding agents and contributors operating in this repository. Read `README.md`, `docs/architecture.md` and relevant source before editing.

## Project boundaries

- This is a **reusable Unity 2D WebGL endless-runner template**, currently without a customer deployment. Preserve existing playable behavior, score calculations, obstacle pacing, pause flow and established iframe message schema unless a change explicitly requests otherwise.
- **Required Unity version: 6000.3.18f1** (`ProjectSettings/ProjectVersion.txt`). Do not upgrade Unity, Test Framework or packages without explicit approval.
- `main` is the delivery baseline. **Work on a new branch and submit a PR. Never push to `main` or merge a PR without the owner's explicit instruction.**
- Keep PRs focused: avoid wide formatting changes, unrelated bug fixes, large refactors or introducing new dependencies where a small change suffices.
- Never treat locally passing tests from an older commit as evidence that the current PR was verified.

## Unity file safety

- Preserve all `.meta` files and GUIDs. Every new asset, script and imported folder requires a corresponding Unity `.meta`; do not regenerate existing GUIDs.
- Do not casually edit `.unity`, `.prefab`, `.asset`, `.inputactions`, animation files, `ProjectSettings/` or `Packages/`. Describe and review serialized changes explicitly.
- Never commit generated `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/` or local IDE artifacts.
- **Inspector values override script defaults** for serialized fields. Check the actual GameScene references, not just C# initializers.
- Do not rename scene GameObjects, UnityEvent targets or `DinoRunWebGLBridge`: its name is used by JavaScript `SendMessage` and is created by `ManagerBootstrapper`.
- `ManagerBootstrapper` creates persistent managers before scene load; preserve singleton identity and proper cleanup/subscription behavior across restarts.

## Game invariants to protect

- Build scene order: `Menu` → `GameScene`. There is no standalone end-game scene; `MainMenu` shows game-over UI within `GameScene`.
- States: `Menu` → `Playing` ↔ `Paused` → `GameOver` and replay. Pausing freezes time through `Time.timeScale = 0` and resuming restores the previous scale.
- Jump with Up Arrow or upper-half touch/click; slide with Down Arrow or lower-half touch/click **while grounded**. Maintain jump buffering and correct ground contact across tile seams.
- Sliding must preserve the bottom of the player's collider and restore original size/offset and visual pose afterwards. Preserve jump-vs-slide collision avoidance.
- Obstacles: Rock on the ground and flying Bird; `SpikeGenerator` uses recovery times, clearance and viewport-based speed limits. Maintain the collision and despawn safeguards at high speed.
- `PoolManager` reuses obstacles and prevents duplicate returns; stale pooled objects must not survive as active playfield obstacles when scenes reload.
- Score accumulates only while playing. `ScoreManager` stores a float, while UI displays a rounded integer and the WebGL bridge submits an **integer-truncated** score. Do not change these conversions silently.
- Client result eligibility is not proof of anti-cheat security.

## Browser / iframe integration

- Before changes, inspect `Assets/Scripts/DinoRunWebGLBridge.cs`, `Assets/Plugins/WebGL/DinoRunWebGLBridge.jslib` and `docs/webgl-integration.md`.
- Current protocol: request `"get_user_id"`, accept trusted `user_id`, send `{type:"unity_game_result", user_id, score}` on death.
- **Trusted parent origin is intentionally empty in source.** Configure the exact production HTTPS origin per deployment; never use `"*"` or copy a domain from another customer. The value is a code constant in DinoRUN, **not** a scene-serialized field.
- JS must check both `event.origin` and `event.source === window.parent` before calling Unity.
- Never print user IDs or full result payloads to logs.
- The bridge prevents repeated result attempts during a single loaded page, even if the game restarts; modifying that behavior requires explicit requirements for multi-run submissions.
- DinoRUN has **no blur/visibility invalidation**. Do not silently port Sorting_Game's anti-focus behavior.
- `postMessage` success does not confirm host reception, backend persistence or server-side score validity. Test the real host when available.

## Changes and validation

1. Read the requested area and outline affected classes, scenes, prefabs and external integration.
2. Keep gameplay changes isolated from documentation, CI and validator changes when practical.
3. Add focused Unity tests for altered behavior; existing `Assets/Tests/Editor/GameplayRegressionTests.cs` has **15 `[UnityTest]` cases** that enter Play Mode. Avoid claiming they are pure, fast NUnit-only EditMode tests.
4. Run the tests in Unity **6000.3.18f1** if an Editor is available; report the exact run results. Last owner-reported PR #2 baseline: **15/15 passing**, compilation clean and gameplay manually checked.
5. For scene or physics changes, manually verify menu, restart, jump buffering, slide collision/visuals, pause, speed/difficulty and aspect ratios.
6. For bridge changes, verify WebGL iframe request/reply, wrong-origin/source rejection, missing ID and duplicate guard; verify actual backend storage separately. Editor tests cannot substitute for a browser integration test.
7. Inspect the diff for GUID changes, generated content, host origins, sensitive data, unrelated code changes and Unity import side effects.
8. PR descriptions must distinguish checks **executed** from checks **planned** or **not possible**. Do not invent build or CI results.

## Useful source map

- Gameplay: `Assets/Scripts/PlayerScript.cs`, `PlayerSlideVisuals.cs`, `GroundTile.cs`.
- State and scoring: `Assets/Scripts/GameStateManager.cs`, `ScoreManager.cs`, `SpeedManager.cs`.
- Spawning and pooling: `Assets/Scripts/SpikeGenerator.cs`, `SpikeScript.cs`, `PoolManager.cs`.
- Manager bootstrapping: `Assets/Scripts/ManagerBootstrapper.cs`.
- UI: `Assets/Scripts/MainMenu.cs`, `PauseMenuUI.cs`, `RunnerCameraFraming.cs`.
- WebGL: `Assets/Scripts/DinoRunWebGLBridge.cs`, `Assets/Plugins/WebGL/DinoRunWebGLBridge.jslib`.
- Tests: `Assets/Tests/Editor/GameplayRegressionTests.cs`, `DinoRun.RegressionTests.asmdef`.
- Python CI: `.github/workflows/repository-checks.yml`, `scripts/validate_unity_repo.py`, `scripts/tests/` (structural checks only; no Unity test execution).
- Architecture: `docs/architecture.md`; test plan: `docs/test-plan.md`; host protocol: `docs/webgl-integration.md`.

Keep framework extraction and read-only Unity scene validation in **separate scoped PRs**. GitHub Actions is Python-only and cannot prove Unity compilation or gameplay correctness.
