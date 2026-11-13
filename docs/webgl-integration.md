# DinoRUN — WebGL parent iframe integration

This project uses the **same host message schema as Sorting_Game**, adapted for DinoRUN's runtime and scene structure. PR #2 replaces `Application.ExternalCall("SetUnityScore", ...)`; it does not import Sorting_Game's session state, score rules or browser-focus invalidation.

## Required configuration before deployment

Open `Assets/Scripts/DinoRunWebGLBridge.cs` and set `ParentOrigin` to the exact HTTPS **parent-page origin**, for example `https://games.example.com`. This is a **code constant**, not an Inspector override. Do not include a path, query, trailing slash or wildcard. The checked-in default is empty and intentionally disables outbound messaging.

The host must embed the WebGL player in an iframe, serve build files via HTTP(S) and implement its own trusted-origin, user/session and backend authorization checks. **Do not ship a customer build until the intended parent origin is configured and tested.**

## Protocol

```text
Unity ManagerBootstrapper
   --> DinoRunWebGLBridge.Start()
   --> DinoRunRequestUserId(ParentOrigin)
   --> parent.postMessage("get_user_id", ParentOrigin)

Parent (trusted HTTPS origin) -> iframe:
   {user_id:"test-id"} OR compatible plain string/number
   event.origin === ParentOrigin
   event.source === window.parent
   --> SendMessage("DinoRunWebGLBridge","OnUserIdReceived",id)

On game-over collision:
   PlayerScript.Die()
   --> DinoRunWebGLBridge.SendResult((int)score)
   --> parent.postMessage({type:"unity_game_result",user_id,score}, ParentOrigin)
```

### Current payload

```json
{
  "type": "unity_game_result",
  "user_id": "test-user-id",
  "score": 120
}
```

The score is the **truncated integer** `(int)CurrentScore`, not the rounded UI `F0` display. The `user_id` originates from the parent host and must not be logged. The host remains responsible for authenticating and validating both fields.

### Host listener example (illustrative, not production auth)

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
    // Apply server-side authentication, session matching and score checks.
    submitToBackend(event.data);
  }
});
```

## Behavior and limits

- The bridge is created persistently by `ManagerBootstrapper`, under the exact GameObject name `DinoRunWebGLBridge`, which JavaScript `SendMessage` requires.
- Unconfigured or invalid `ParentOrigin` disables user ID requests and result emission. Standalone builds without a parent iframe do not participate in the handshake.
- `.jslib` rejects messages unless both `event.origin` and `event.source` match the trusted parent.
- The bridge stores the received ID locally and never logs the ID or complete payload.
- `SendResult` refuses missing IDs and duplicate attempts, marking the result before calling native code.
- **One result attempt per loaded page:** `resultSent` is not reset when the user chooses Try Again. If a host needs a score for each run, the bridge must be deliberately adapted with per-run identity and tests.
- No transport confirmation, retry or backend acknowledgement exists. A `true` return means local eligibility/attempt only; a host or network failure can still lose the result.
- The Editor cannot execute JSLib; game tests validate gameplay, not host delivery.
- Browser blur does **not** invalidate DinoRUN results. This differs intentionally from Sorting_Game.

## Manual QA before accepting a WebGL deployment

1. Configure a temporary HTTPS parent origin for the build and serve it from a distinct origin in an iframe.
2. Confirm the parent receives `get_user_id` and the child accepts a valid response.
3. Attempt a response from a different origin and another window; neither should reach the game.
4. End a run with a valid ID: observe one result message and compare the **truncated** score with the displayed value.
5. End a run without a valid ID and ensure nothing is sent.
6. End/restart within the same browser page, observe the current single-result limit and decide whether that meets the client brief.
7. Inspect backend persistence, duplicate handling, authentication and error management independently.
8. Keep test user IDs disposable; ensure browser/Unity logs do not expose identifiers.

For the full testing matrix and release checklist see `docs/test-plan.md`.
