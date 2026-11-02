using System;
using UnityEngine;

public enum GameState { Menu, Playing, Paused, GameOver }

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Menu;

    public event Action<GameState> OnStateChanged;
    private float timeScaleBeforePause = 1f;

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

    public void SetState(GameState newState)
    {
        if (State == newState) return;
        if (newState == GameState.Paused)
        {
            if (State != GameState.Playing) return;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }
        else if (State == GameState.Paused)
            Time.timeScale = timeScaleBeforePause;
        State = newState;
        OnStateChanged?.Invoke(State);
    }

    public void TogglePause()
    {
        if (State == GameState.Playing) SetState(GameState.Paused);
        else if (State == GameState.Paused) SetState(GameState.Playing);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (State == GameState.Paused) Time.timeScale = timeScaleBeforePause;
        Instance = null;
    }
}
