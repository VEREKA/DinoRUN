# DinoRUN — WebGL parent iframe integration

This project uses the **same message contract as Sorting_Game**, adapted for DinoRUN. It replaces the deprecated `Application.ExternalCall("SetUnityScore", ...)` without importing Sorting_Game-specific scoring, session state or focus-invalidating behavior.

## Required configuration before deployment

Open `Assets/Scripts/DinoRunWebGLBridge.cs` and set `ParentOrigin` to the exact **HTTPS origin** of the parent page, for example `https://games.example.com`. Do not include a path, query, trailing slash, or wildcard. The checked-in default is an empty string and intentionally disables messages. **Do not ship until you have configured and verified the intended host.**

The host iframe must also have an appropriate `src`, serve WebGL assets over HTTP(S), and implement the listener/response flow below.

## Protocol

1. The bridge requests the user ID after bootstrapping. Its `.jslib` sends `"get_user_id"` via `window.parent.postMessage("get_user_id", ParentOrigin)`.
2. The host replies with `{user_id:"test-id"}`, or compatible plain string/number, using the **child iframe origin** as `targetOrigin`.
3. The `.jslib` rejects messages not from the configured `event.origin` or `event.source !== window.parent`. It invokes `SendMessage("DinoRunWebGLBridge", "OnUserIdReceived", id)` for an accepted ID.
4. A normal DinoRUN player death attempts one `{type:"unity_game_result", user_id:"test-id", score:42}` message using the same trusted `targetOrigin`.

```js
// Example host listener only; real backend identity checks are still required.
window.addEventListener("message", (event) => {
  if (event.origin !== TRUSTED_DINORUN_IFRAME_ORIGIN) return;
  if (event.source !== gameIframe.contentWindow) return;

  if (event.data === "get_user_id") {
    gameIframe.contentWindow.postMessage(
      { user_id: currentUserId },
      TRUSTED_DINORUN_IFRAME_ORIGIN
    );
  } else if (event.data?.type === "unity_game_result") {
    // Validate session and score server-side; avoid duplicate submissions.
    submitToBackend(event.data);
  }
});
```

## Behavior and limits

- `DinoRunWebGLBridge` is created persistently in `ManagerBootstrapper` with a fixed name for JavaScript `SendMessage` callbacks. No serialized scene changes are needed.
- The bridge requires both the trusted origin and a valid received ID. It does not log user IDs.
- A result is marked as sent before invoking the WebGL function, preventing duplicates during one app session. It does not reset when gameplay restarts. A page refresh restarts the session.
- The existing DinoRUN flow sends the **truncated integer** score, matching the old `(int)` cast; no rounding or scoring mechanics changed.
- The Unity Editor cannot exercise real iframe messages. **The host must be tested in a WebGL build**.
- The child has no acknowledgement/retry; `true` from `SendResult` is only eligibility/attempt, not proof of host receipt or stored result.
- A standalone build without an iframe is intentionally not capable of host submission.
- DinoRUN does **not** automatically invalidate runs when focus is lost. That behavior belonged to a different production game and is not added here.

## Manual QA before accepting this change

1. Set a test trusted origin and build WebGL.
2. Open in iframe, confirm `get_user_id` reaches the host and valid ID is received.
3. Try a message from a different window and different origin; neither should be accepted.
4. Die once and confirm the host receives one integer score; reload and repeat.
5. Die without a valid ID and verify no result is received.
6. Validate backend persistence and server-side identity/duplicate controls independently.
