using UnityEngine;

public static class ManagerBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureManagers()
    {
        // GameStateManager
        if (GameStateManager.Instance == null)
        {
            var go = new GameObject("GameStateManager");
            go.AddComponent<GameStateManager>();
            Object.DontDestroyOnLoad(go);
        }

        // ScoreManager
        if (ScoreManager.Instance == null)
        {
            var go = new GameObject("ScoreManager");
            go.AddComponent<ScoreManager>();
            Object.DontDestroyOnLoad(go);
        }

        // SpeedManager
        if (SpeedManager.Instance == null)
        {
            var go = new GameObject("SpeedManager");
            go.AddComponent<SpeedManager>();
            Object.DontDestroyOnLoad(go);
        }

        // PoolManager
        if (PoolManager.Instance == null)
        {
            var go = new GameObject("PoolManager");
            go.AddComponent<PoolManager>();
            Object.DontDestroyOnLoad(go);
        }

        // WebGL bridge uses a stable GameObject name for the JavaScript callback.
        if (DinoRunWebGLBridge.Instance == null)
        {
            var go = new GameObject("DinoRunWebGLBridge");
            go.AddComponent<DinoRunWebGLBridge>();
            Object.DontDestroyOnLoad(go);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDebugHUD()
    {
        // Scene objects are available here; managers must be created earlier.
        if (Object.FindFirstObjectByType<DebugHUD>() == null)
        {
            var hud = new GameObject("DebugHUD");
            hud.AddComponent<DebugHUD>();
            Object.DontDestroyOnLoad(hud);
        }
    }
#endif
}
