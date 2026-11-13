# DinoRUN — local Unity scene and prefab validator

This is a **read-only, Editor-only** validation tool analogous to Sorting_Game's local validator, but adapted to DinoRUN's real scene structure and runtime fallbacks.

## Run the validator

1. Check out the PR #4 branch (or `main` after the PR is merged), and open the project in **Unity 6000.3.18f1**.
2. Let compilation/import finish, then stop Play Mode.
3. Save unrelated, unsaved scene edits before validation as a precaution.
4. Choose **Tools → VEREKA → Validate DinoRUN**.
5. Read the Unity Console entries starting with `[DinoRUN Validator]`. A summary reports **checks, errors and warnings**.

The command uses `EditorSceneManager.OpenScene(..., Additive)` to inspect scenes not already loaded, and closes only scenes it opened. It does not save project scenes or prefabs. Errors and warnings require human review; nothing is automatically fixed.

## What is checked?

| Scope | Validation |
| --- | --- |
| Build Settings | Exactly two enabled scenes, ordered `Menu` then `GameScene` |
| Both scenes | One `MainCamera`-tagged camera, at least one Canvas/EventSystem, one MainMenu controller, no missing MonoBehaviour scripts |
| Menu | `MainMenu.startButton` must be assigned; other in-game UI fields can be empty |
| GameScene UI | MainMenu's TryAgain, end overlay, live/final score, game-over, start instructions/arrows references |
| Player | One PlayerScript with Rigidbody2D, CapsuleCollider2D, Animator, PlayerSlideVisuals, SpriteRenderer; positive JumpForce, slideDuration and scoreMultiplier |
| Camera | One RunnerCameraFraming attached to Camera; player available either through Inspector or runtime fallback |
| Generator | One SpikeGenerator; Bird/Rock prefab references; valid distance bounds and top-spawn chance; positive reaction time |
| Generator player | Null serialized `player` is **valid** when the scene has exactly one PlayerScript, because the generator resolves it at runtime |
| World | At least two GroundTile components; Finish-tagged trigger collider for obstacle recycling |
| Bird / Rock prefabs | Present, tagged `spike`, with SpikeScript, Rigidbody2D, Collider2D, SpriteRenderer and no missing scripts |
| Slide visuals | Exactly one PlayerSlideVisuals with assigned sparkSprite |

The validator checks that referenced objects are assigned and relevant configuration values are plausible. It does not require DinoRUN's exported game-object managers in a scene: `ManagerBootstrapper` creates `GameStateManager`, `ScoreManager`, `SpeedManager`, `PoolManager` and `DinoRunWebGLBridge` at runtime.

## Expected results, evidence and limitations

The previous repository scan found a populated game scene with assigned Bird/Rock prefabs, while `SpikeGenerator.player` is intentionally unassigned. Unlike the starting-lives check in Sorting_Game, these rules mainly check presence and sensible configuration ranges rather than hardcoding arbitrary authoring numbers.

**No Unity Editor validation result is claimed for PR #4 until it is run locally.** Record the exact reported check/error/warning counts in the PR before approving it. Run the existing **15 Unity regression tests** separately if you want additional assurance that the editor scripts compile and gameplay remains intact.

Python GitHub Actions checks `.meta` GUIDs, scene/build order, JSON and file presence but **cannot load scenes in Unity**, inspect live components or run this Editor validator. Passing CI is not equivalent to a local Unity validation pass. Likewise, a green scene validation result cannot guarantee gameplay behavior, WebGL host messaging or backend persistence.

## Review checklist

1. Confirm the new Editor script compiles in Unity 6000.3.18f1.
2. Run the validator on Menu and GameScene; inspect **all** errors and warnings, not only the summary.
3. Confirm no scene or prefab is marked dirty by the validator and Git shows no unexpected serialized changes.
4. Optionally remove a required reference in a disposable local copy, verify the error is detected, then revert.
5. If all checks pass, report the check count and follow up with the 15 Unity regression tests or a short manual gameplay smoke test.

The local tool is stored in `Assets/Editor/DinoRunSceneValidator.cs` and runs only inside Unity Editor.
