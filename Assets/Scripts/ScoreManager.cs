using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public float CurrentScore { get; private set; }
    public float ScorePerSecond { get; set; } = 4f;
    private int lastDisplayedScore;

    public event Action<float> OnScoreChanged;

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

    public void UpdateScore(float score)
    {
        CurrentScore = score;
        int displayedScore = Mathf.RoundToInt(CurrentScore);
        if (displayedScore == lastDisplayedScore) return;
        lastDisplayedScore = displayedScore;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    private void Update()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.State == GameState.Playing)
            UpdateScore(CurrentScore + Time.deltaTime * ScorePerSecond);
    }

    public void ResetScore()
    {
        CurrentScore = 0f;
        lastDisplayedScore = 0;
        OnScoreChanged?.Invoke(CurrentScore);
    }
}
