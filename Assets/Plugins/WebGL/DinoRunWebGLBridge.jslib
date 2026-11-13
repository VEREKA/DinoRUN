mergeInto(LibraryManager.library, {
  DinoRunRequestUserId: function (originPtr) {
    var origin = UTF8ToString(originPtr);
    if (!origin || !window.parent || window.parent === window) {
      console.warn("DinoRUN: iframe parent or trusted origin unavailable.");
      return;
    }
    // Only allow exact, configured HTTPS origins; never use "*".
    var parsed;
    try { parsed = new URL(origin); } catch (e) { return; }
    if (parsed.protocol !== "https:" || parsed.origin !== origin) return;

    window.__dinoRunTrustedParentOrigin = origin;
    if (!window.__dinoRunUserIdListenerRegistered) {
      window.addEventListener("message", function (event) {
        if (event.origin !== window.__dinoRunTrustedParentOrigin) return;
        if (event.source !== window.parent) return;
        var data = event.data;
        var id = null;
        if (data && typeof data === "object" && data.user_id != null) {
          id = String(data.user_id);
        } else if (typeof data === "string" || typeof data === "number") {
          if (data !== "get_user_id") id = String(data);
        }
        if (!id || !id.trim()) return;
        SendMessage("DinoRunWebGLBridge", "OnUserIdReceived", id);
      });
      window.__dinoRunUserIdListenerRegistered = true;
    }
    window.parent.postMessage("get_user_id", origin);
  },

  DinoRunSendPlayerResult: function (userIdPtr, score, originPtr) {
    var origin = UTF8ToString(originPtr);
    if (!window.parent || window.parent === window ||
        origin !== window.__dinoRunTrustedParentOrigin) {
      console.warn("DinoRUN: result not sent (no trusted iframe parent).");
      return;
    }
    var payload = {
      type: "unity_game_result",
      user_id: UTF8ToString(userIdPtr),
      score: score | 0
    };
    // Never log identifiers or the complete payload.
    window.parent.postMessage(payload, origin);
  }
});
