using System;
using UnityEngine;

public class SpeedManager : MonoBehaviour
{
    public static SpeedManager Instance { get; private set; }

    [Header("Speed Settings")]
    public float minSpeed = 5f;
    public float maxSpeed = 90f;
    [Tooltip("Legacy multiplier (ignored when using curve). Kept for compatibility.")]
    public float speedMultiplier = 0.5f;

    [Header("Difficulty Curve")]
    [Tooltip("Use score (true) or time since level load (false) to sample the difficulty curve")]
    public bool useScoreForProgress = true;
    [Tooltip("Value (score or seconds) at which curve reaches 1.0 (max difficulty)")]
    public float progressToMax = 600f;
    [Tooltip("Defines how speed grows from min to max. X axis is normalized progress (0..1).")]
    public AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 0.05f),
        new Keyframe(0.5f, 0.35f),
        new Keyframe(0.8f, 0.75f),
        new Keyframe(1f, 1f)
    );

    public float CurrentSpeed { get; private set; }
    public float CurrentNormalized { get; private set; }
    public float GameplaySpeedLimit { get; private set; } = float.PositiveInfinity;

    public event Action<float> OnSpeedChanged;
    [Header("Debug")]
    public bool enableLogs = false;
    private float playingTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);

        CurrentSpeed = minSpeed;
    }

    private void Update()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.State != GameState.Playing)
            return;
        playingTime += Time.deltaTime;
        float progressValue = 0f;
        if (useScoreForProgress)
        {
            progressValue = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0f;
        }
        else
        {
            progressValue = playingTime;
        }

        float t = progressToMax > 0f ? Mathf.Clamp01(progressValue / progressToMax) : 0f;
        CurrentNormalized = t;

        float curveSample = speedCurve != null ? speedCurve.Evaluate(t) : t;
        float newSpeed = Mathf.Min(Mathf.Lerp(minSpeed, maxSpeed, curveSample), GameplaySpeedLimit);

        if (!Mathf.Approximately(newSpeed, CurrentSpeed))
        {
            CurrentSpeed = newSpeed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableLogs) Debug.Log($"[SpeedManager] NewSpeed={CurrentSpeed:F2} t={t:F2} curve={curveSample:F2}");
#endif
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }
    }

    public void ResetSpeed()
    {
        playingTime = 0f;
        GameplaySpeedLimit = float.PositiveInfinity;
        CurrentSpeed = minSpeed;
        CurrentNormalized = 0f;
        OnSpeedChanged?.Invoke(CurrentSpeed);
    }

    public void SetSpeed(float speed)
    {
        CurrentSpeed = Mathf.Min(Mathf.Clamp(speed, minSpeed, maxSpeed), GameplaySpeedLimit);
        OnSpeedChanged?.Invoke(CurrentSpeed);
    }

    public void SetGameplaySpeedLimit(float speedLimit)
    {
        GameplaySpeedLimit = Mathf.Max(0.1f, speedLimit);
        if (CurrentSpeed > GameplaySpeedLimit)
        {
            CurrentSpeed = GameplaySpeedLimit;
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }
    }
}
