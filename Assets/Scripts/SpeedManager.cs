using System;
using UnityEngine;

public class SpeedManager : MonoBehaviour
{
    public static SpeedManager Instance { get; private set; }

    [Header("Speed Settings")]
    public float minSpeed = 5f;
    public float maxSpeed = 18f;
    public float speedMultiplier = 0.12f;

    public float CurrentSpeed { get; private set; }

    public event Action<float> OnSpeedChanged;

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
        // Przyspieszamy tylko w trakcie gry - nie w menu, pauzie ani po Game Over.
        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing)
            return;

        if (CurrentSpeed < maxSpeed)
        {
            CurrentSpeed += speedMultiplier * Time.deltaTime;
            if (CurrentSpeed > maxSpeed) CurrentSpeed = maxSpeed;
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }
    }

    public void ResetSpeed()
    {
        CurrentSpeed = minSpeed;
        OnSpeedChanged?.Invoke(CurrentSpeed);
    }

    public void SetSpeed(float speed)
    {
        CurrentSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        OnSpeedChanged?.Invoke(CurrentSpeed);
    }
}
