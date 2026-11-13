using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// DinoRUN iframe bridge. The parent origin must be explicitly configured for
/// each deployment. The JavaScript callbacks require the GameObject to be named
/// DinoRunWebGLBridge (created by ManagerBootstrapper).
/// </summary>
public sealed class DinoRunWebGLBridge : MonoBehaviour
{
    public static DinoRunWebGLBridge Instance { get; private set; }

    // Set to the exact, trusted HTTPS parent-page origin before shipping WebGL.
    // Example format: https://games.example.com (no path or trailing slash).
    // An empty value intentionally disables external messaging.
    private const string ParentOrigin = "";

    private string userId;
    private bool resultSent;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DinoRunRequestUserId(string origin);

    [DllImport("__Internal")]
    private static extern void DinoRunSendPlayerResult(string userId, int score, string origin);
#endif

    private static bool HasTrustedOrigin()
    {
        Uri uri;
        return Uri.TryCreate(ParentOrigin, UriKind.Absolute, out uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.GetLeftPart(UriPartial.Authority) == ParentOrigin;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RequestUserId();
    }

    public void RequestUserId()
    {
        if (!HasTrustedOrigin())
        {
            Debug.LogWarning("DinoRunWebGLBridge: trusted parentOrigin is not configured; messaging disabled.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(userId))
            return;

#if UNITY_WEBGL && !UNITY_EDITOR
        DinoRunRequestUserId(ParentOrigin);
#else
        Debug.Log("DinoRunWebGLBridge: user_id request is WebGL-only.");
#endif
    }

    // Called by JSLib's SendMessage after origin/source validation.
    public void OnUserIdReceived(string value)
    {
        if (!HasTrustedOrigin() || string.IsNullOrWhiteSpace(value))
        {
            Debug.LogWarning("DinoRunWebGLBridge: ignored missing or untrusted user_id.");
            return;
        }

        userId = value;
        Debug.Log("DinoRunWebGLBridge: user_id received.");
    }

    public bool SendResult(int score)
    {
        if (resultSent)
        {
            Debug.LogWarning("DinoRunWebGLBridge: duplicate result ignored.");
            return false;
        }

        if (!HasTrustedOrigin() || string.IsNullOrWhiteSpace(userId))
        {
            Debug.LogWarning("DinoRunWebGLBridge: result not sent (missing trusted origin or user_id).");
            return false;
        }

        // Mark before invoking native code so repeated death callbacks cannot submit twice.
        resultSent = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        DinoRunSendPlayerResult(userId, score, ParentOrigin);
#else
        Debug.Log($"DinoRunWebGLBridge mock: eligible result, score={score}.");
#endif
        // This only confirms an attempted postMessage, NOT backend receipt/storage.
        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
