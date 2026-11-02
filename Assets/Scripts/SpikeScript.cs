using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpikeScript : MonoBehaviour
{
    public SpikeScript prefabSource;
    [Tooltip("Fallback return boundary if a fast obstacle skips the Finish trigger")]
    [SerializeField] private float despawnX = -12f;

    public void Initialize()
    {
        rb.position = transform.position;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private Rigidbody2D rb;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private SpikeDebugState debugState;
#endif

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugState = GetComponent<SpikeDebugState>();
#endif
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void FixedUpdate()
    {
        if (rb.position.x < despawnX)
        {
            ReturnToPool();
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float speed = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 5f;
        rb.MovePosition(rb.position + Vector2.left * speed * Time.fixedDeltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugState != null && debugState.enableLogs) debugState.MaybeLog(speed, gameObject.name);
#endif
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Finish"))
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        // Multiple callbacks in one physics step must not enqueue the same instance twice.
        if (!gameObject.activeSelf) return;
        rb.linearVelocity = Vector2.zero;
        if (PoolManager.Instance != null)
            PoolManager.Instance.Return(this);
        else
            Destroy(gameObject);
    }
}
