# DinoRUN — verification plan and current coverage

This document separates **implemented tests**, **owner-reported results**, and **future checks**. A passing run is historical evidence for the tested branch, not automatic proof that later changes or browser deployments work.

## Implemented checks and last reported results

| Check | Implementation | Last reported result | Where it runs |
| --- | --- | --- | --- |
| Scene/gameplay regression tests | `Assets/Tests/Editor/GameplayRegressionTests.cs` — 18 `[UnityTest]` cases (15 existing + 3 new) | **15/15 passed** locally before PR #5; **3 additional contract tests await a local run** (expected total 18) | Unity 6000.3.18f1 Test Runner, EditMode suite entering Play Mode |
| Compilation and gameplay smoke test | Owner opened and played the project | Clean compilation; gameplay working as expected for PR #2 | Unity Editor |
| WebGL iframe handshake and result delivery | `DinoRunWebGLBridge.cs` and `.jslib` | **Not tested** with an actual host; `ParentOrigin` intentionally blank | WebGL build in an HTTPS iframe |
| Repository structural checks | `.github/workflows/repository-checks.yml` and 17 Python self-tests | **Implemented in PR #3; GitHub run pending** | Python-only GitHub Actions |
| Read-only scene/prefab validator | `Assets/Editor/DinoRunSceneValidator.cs` | **Added in PR #4; awaiting local Unity run** | Unity Editor → Tools → VEREKA → Validate DinoRUN |

The Unity tests run through the `EditMode` Test Runner but call `EnterPlayMode` and exercise scene lifecycle, physics and UI. They are **not** pure-C# arithmetic tests and are not executed by GitHub Actions.

## Test environments

- **Unity Editor:** `6000.3.18f1` with packages from `Packages/manifest.json`. Run **Window → General → Test Runner → EditMode → Run All**. Tests that enter Play Mode can be slower and may change resolution/camera state.
- **Local manual play:** test Menu and GameScene at desktop and narrow/portrait aspect ratios; use keyboard, mouse and touch where possible.
- **WebGL:** build with WebGL Support installed. Serve from HTTP(S), embed in a trusted parent iframe and verify actual host-side messages. Browser security, session storage and backend receipt are beyond an Editor-only test.
- **CI:** Python-only workflow checks repository integrity, but not Unity compilation or runtime; see `docs/ci-setup.md`.

## EditMode coverage and planned tests

The following cases are implemented in `GameplayRegressionTests.cs`. The last three were added in PR #5 and are **not yet locally verified**:

| ID | Existing regression case | Validates |
| --- | --- | --- |
| E01 | `DirectStartAndRepeatedSceneLoadsKeepExactlyOneSubscription` | Manager uniqueness and event subscriptions across scene reloads |
| E02 | `FastObstacleCrossingPlayerStillEndsTheRun` | High-speed collision detection |
| E03 | `MissedFinishTriggerReturnsAndReusesTheObstacleOnce` | Despawn fallback and pool reuse |
| E04 | `AlternatingObstaclesAllowRecoveryAtLowAndHighSpeeds` | Spacing and action recovery under speed changes |
| E05 | `NarrowerViewportLimitsSpeedToPreserveReactionTime` | Viewport-dependent speed cap |
| E06 | `RepeatedSlidePreservesFeetAndDoesNotShortenTheNextSlide` | Correct collider alignment and reset |
| E07 | `PauseFreezesPhysicsScoreDifficultyAndSlideWithoutResettingOnResume` | Pause behavior and resumed timers |
| E08 | `ScoreHasOneOwnerAndOnlyNotifiesWhenDisplayedValueChanges` | Score events, UI text and stop conditions |
| E09 | `PoolKeepsActiveAndInactiveInstancesAcrossSceneRestarts` | Pool state across replay |
| E10 | `DiagnosticsStayOffAndDoNotAddHelpersDuringGameplay` | Disabled debug helpers |
| E11 | `JumpPressedBeforeLandingExecutesOnceAndExpiredInputDoesNotJump` | Jump buffering and expiry |
| E12 | `GroundContactSurvivesASeamAndClearsWhenTheLastSupportDisappears` | Ground-contact behavior |
| E13 | `PauseButtonsWorkWithoutKeyboardAndFitPortraitScreens` | Pointer pause/resume and responsive layout |
| E14 | `SlideClearsFlyingObstacleButStandingDoesNot` | Slide vs flying obstacle collision |
| E15 | `SlideChangesOnlyArtworkAndRestoresTheRunPose` | Artwork/animation vs collider stability |
| E16 (PR #5) | `SerializedPlayerTuningMatchesTemplateContract` | Literal scene tuning: jump 12, slide 0.4 s, jump buffer 0.12 s, score 4/s |
| E17 (PR #5) | `ScoreAccrualUsesFourPointsPerSecondAndStopsOutsidePlaying` | Accrual at known delta, freeze on pause/end, score reset |
| E18 (PR #5) | `DifficultyCurveHonorsFixedMilestonesSpeedCapAndReset` | Curve speed at score 0/300/600, viewport cap and reset |

`GameplayRegressionTests.cs` also contains screenshot helper code which may write to `Logs/GameplayVisuals/` (or the `DINORUN_TEST_OUTPUT` directory). Do not commit screenshot output or other test artifacts.

### Suggested additional tests (not implemented)

- P01: Extend the Unity Scene Validator with further serialized references when the project changes. PR #5 adds regression assertions for the actual scene's four PlayerScript numeric values.
- P02: Keep the newly tested **template tuning values** under review when adapting DinoRUN for customers. Deliberate changes require updating numeric-contract tests and documenting the reason.
- P03: Test bridge no-origin/no-ID/duplicate behavior separately; JavaScript origin/source tests require a browser.
- P04: Verify repeated gameplay runs with a persistent bridge **if the customer expects one score per replay**; currently the second result is suppressed until page reload.

## WebGL iframe / protocol matrix

| ID | Scenario | Expected result | Status |
| --- | --- | --- | --- |
| W01 | Correct configured parent origin and user ID | Child sends `get_user_id`, accepts host response | Not run |
| W02 | Wrong origin sends user ID | Ignore message | Not run |
| W03 | Correct origin, wrong `event.source` | Ignore message | Not run |
| W04 | Bare string/number user ID from the correct host | Accept supported data shape | Not run |
| W05 | Empty/unconfigured `ParentOrigin` | Do not request or submit | Code reviewed; browser not run |
| W06 | Standalone WebGL without iframe | No host result submission | Not run |
| W07 | Death without accepted ID | No result | Not run |
| W08 | Death with accepted ID | Emit `{type:"unity_game_result",user_id,score}` with truncated integer score | Not run |
| W09 | Duplicate death in one page session | No second result | Not run |
| W10 | Replay inside same page | Current bridge suppresses later result; confirm desired business requirement | Not run |
| W11 | Browser tab focus changes | Runner has no focus-invalidating anti-cheat rule | Not run |
| W12 | Parent receives message but backend fails | Do not claim persistence; host must handle failure | Not run |

Test with disposable host IDs. Never log real user identifiers, credentials or full score payloads. For the host's implementation details, see `docs/webgl-integration.md`.

## Manual release checklist

- [ ] Unity imports with no unexpected compiler errors or missing packages.
- [ ] All 18 regression cases pass on the release branch (15 previously confirmed, 3 new in PR #5).
- [ ] Menu start → instructions → keyboard/touch start works.
- [ ] Jump and buffered jump work; expired input does not jump late.
- [ ] Ground rocks require jumping; flying birds can be avoided by sliding.
- [ ] Standing and sliding collider geometry, visual pose and reset behave normally.
- [ ] Score increases only while playing and final result is displayed.
- [ ] Difficulty progression and obstacle pacing remain playable at low/high speeds.
- [ ] Pause/resume works using keyboard and buttons, without unexpected score/time progression.
- [ ] Restart works repeatedly with no duplicate managers, callbacks or leaked obstacles.
- [ ] Desktop and portrait UI remain usable; player is visible at narrow viewports.
- [ ] No debug UI/log spam, unintentional generated files or GUID changes.
- [ ] Deployment origin is configured specifically for the chosen host.
- [ ] Actual WebGL parent iframe messaging is tested, including incorrect origin/source, missing ID and result submission.
- [ ] Backend receives, validates and persists eligible results; duplicate behavior approved.
- [ ] No embedded private secrets, unapproved client domains or unsafe `targetOrigin:"*"`.

## Future verification work (optional, separate PRs)

1. Maintain the Python-only GitHub Actions checks; keep `.meta` / scene integrity self-tests up to date.
2. Keep the local Unity scene/prefab validator up to date with scene reference changes; review its output after modifying serialized assets.
3. Maintain the new baseline numeric score/difficulty tests; add more isolated tests only when a specific customer requirement warrants them.
4. Once a host exists, test actual WebGL messaging and design per-run result submission/retry policy if required.

## Evidence and reporting template

For every PR with behavioral impact, record Unity editor version, branch/commit, exact Test Runner suite and pass/fail count, manual scenario results, WebGL deployment origin/environment (without sensitive IDs), and CI results (if any). A skipped check is **unverified**, not passing.
