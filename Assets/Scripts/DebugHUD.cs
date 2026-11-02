using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    public bool show = false;
    public int fontSize = 14;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private GUIStyle style;

    private void OnGUI()
    {
        if (!show) return;
        if (style == null)
        {
            style = new GUIStyle();
            style.normal.textColor = Color.white;
        }
        style.fontSize = fontSize;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");

        float speed = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 0f;
        float norm = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentNormalized : 0f;
        float score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0f;
        string state = GameStateManager.Instance != null ? GameStateManager.Instance.State.ToString() : "-";
        int smId = SpeedManager.Instance != null ? SpeedManager.Instance.GetInstanceID() : 0;

        GUILayout.Label($"State: {state}", style);
        GUILayout.Label($"Speed: {speed:F2}", style);
        GUILayout.Label($"Normalized: {norm:F2}", style);
        GUILayout.Label($"Score: {score:F0}", style);
        GUILayout.Label($"SpeedManager ID: {smId}", style);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
#endif
}
