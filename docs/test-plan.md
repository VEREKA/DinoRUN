# DinoRUN — regression plan

## Existing automated tests

`Assets/Tests/Editor/GameplayRegressionTests.cs` defines **15 UnityTest cases**. They use `EnterPlayMode` to exercise actual objects and scenes; they are not simple pure-C# EditMode arithmetic tests. At the time of PR #2 preparation, **they had not been executed in the assistant environment**; the owner must run the full suite in Unity 6000.3.18f1.

Existing cases cover:
- Manager singleton/subscription lifecycles through repeated scene reloads.
- Fast obstacle collisions and obstacle pool returning/reusing instances.
- Alternating obstacle spacing and reaction time, including differing viewport speed limits.
- Slide collider feet alignment, repeated slides, visual run/slide transitions and collision survival.
- Pause freezes game mechanics; keyboard/pointer pause UI, portrait/landscape sizing.
- Score ownership and UI event notifications.
- Jump buffering and ground-contact seams.
- Diagnostic HUD stays disabled, and visuals screenshot helper output.

The tests sometimes set screen resolution, camera aspect, physics simulation mode, and test output screenshots. Review changes carefully and rerun in the Editor. `UNITY_INCLUDE_TESTS` is not used as a required define constraint here: the test assembly is recognized by references to Unity's Test Runner assemblies.

## Manual gameplay smoke tests

- [ ] Menu → GameScene, initial waiting state, start on keyboard/pointer input
- [ ] Score increases while running and remains unchanged in paused/menu/game-over state
- [ ] Jump, buffered jump, touch/click on upper half; slide and lower-half touch/click
- [ ] Rock collision/death and bird avoidance while sliding
- [ ] Pause / resume / return; UI at desktop and portrait aspect ratios
- [ ] Restart multiple times; no duplicate managers, event handlers, or stale obstacles
- [ ] Spawning and speed progression over time; player remains visible at narrow aspect ratio
- [ ] Game-over UI shows final score

## WebGL integration matrix (requires deployed build + mock/real host)

| Case | Expected |
| --- | --- |
| Embedded on configured HTTPS origin | Child requests `get_user_id`; host responds with `user_id` |
| Untrusted `event.origin` | Ignore user ID |
| Matching origin but wrong `event.source` | Ignore user ID |
| No iframe or `ParentOrigin` empty | No user ID/result messaging |
| Die without accepted user ID | No result emitted |
| Die with accepted user ID | One `unity_game_result` with integer score |
| Repeated game over callback | No second result from same bridge instance |
| Parent listener absent or backend fails | Child does not receive delivery acknowledgement; independently inspect backend |

Check browser console and server persistence separately. Do not count JavaScript `postMessage` as proof the server accepted the score. Test with a nonproduction user ID.

## Next verification tooling

- Repository CI checks (Python-only, no Unity license): separate PR.
- Unity read-only scene/prefab validator: separate PR.
- Optional unit tests for pure scoring/configuration contracts: separate, gameplay-scoped PR.
