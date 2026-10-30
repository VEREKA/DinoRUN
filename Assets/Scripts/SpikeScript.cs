using UnityEngine;

public class SpikeScript : MonoBehaviour
{
    public SpikeScript prefabSource;

    [Tooltip("Awaryjny powrót do puli, gdyby kolec minął trigger 'Finish'")]
    [SerializeField] private float despawnX = -40f;

    public void Initialize()
    {
        // kept for API compatibility; no per-spike generator needed anymore
    }

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void OnEnable()
    {
        // Po wyjęciu z puli zsynchronizuj ciało fizyczne z nową pozycją,
        // żeby interpolacja nie "przeciągnęła" kolca ze starego miejsca.
        if (rb != null)
            rb.position = transform.position;
    }

    private static bool IsPlaying =>
        GameStateManager.Instance == null || GameStateManager.Instance.State == GameState.Playing;

    private static float Speed =>
        SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 5f;

    // Z Rigidbody2D: ruch przez fizykę (MovePosition w FixedUpdate).
    private void FixedUpdate()
    {
        if (rb == null || !IsPlaying)
            return;

        Vector2 next = rb.position + Vector2.left * (Speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (next.x < despawnX)
            ReturnToPool();
    }

    // Bez Rigidbody2D: zachowanie jak dotychczas.
    private void Update()
    {
        if (rb != null || !IsPlaying)
            return;

        transform.Translate(Vector2.left * Speed * Time.deltaTime);

        if (transform.position.x < despawnX)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Finish"))
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (!gameObject.activeSelf)
            return;

        if (PoolManager.Instance != null)
            PoolManager.Instance.Return(this);
        else
            Destroy(gameObject);
    }
}
