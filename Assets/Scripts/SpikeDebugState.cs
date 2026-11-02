using UnityEngine;

public class SpikeDebugState : MonoBehaviour
{
    public bool enableLogs = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float lastSpeed = -1f;
    private float lastLogTime = 0f;
#endif

    public void MaybeLog(float speed, string objName)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableLogs) return;
        if (Mathf.Abs(speed - lastSpeed) > 0.01f && Time.time - lastLogTime > 0.5f)
        {
            int smId = SpeedManager.Instance != null ? SpeedManager.Instance.GetInstanceID() : 0;
            Debug.Log($"[Spike] {objName} speed sample: {speed:F2} (SpeedManager ID: {smId})");
            lastSpeed = speed;
            lastLogTime = Time.time;
        }
#endif
    }
}
