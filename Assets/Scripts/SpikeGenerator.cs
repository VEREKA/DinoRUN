using UnityEngine;

public class SpikeGenerator : MonoBehaviour
{
    [Header("Spike Prefabs")]
    [SerializeField] private SpikeScript bottomSpikePrefab;
    [SerializeField] private SpikeScript topSpikePrefab;

    [Header("Spike Settings")]
    [Tooltip("Y position for top spike spawn (local to generator)")]
    [SerializeField] private float topSpikeY = -3f;
    [Tooltip("Y position for bottom spike spawn (local to generator)")]
    [SerializeField] private float bottomSpikeY = -4f;
    [Tooltip("Chance (0..1) to spawn top spike instead of bottom")]
    [SerializeField] private float chanceForTopSpawn = 0.3f;


    [Header("Distance Settings")]
    [Tooltip("Minimum horizontal distance between spawned spikes")]
    [SerializeField] private float minDistance = 6f;
    [Tooltip("Maximum horizontal distance between spawned spikes")]
    [SerializeField] private float maxDistance = 15f;
    [Tooltip("Random offset applied to distance to add variety")]
    [SerializeField] private float randomOffset = 0.8f;

    [SerializeField] private float baseMinSafeDistance = 7f;
    [SerializeField] private float maxMinSafeDistance = 12f;

    [Header("Reaction and Recovery")]
    [SerializeField] private PlayerScript player;
    [Tooltip("Minimum time an obstacle is visible before reaching the player")]
    [SerializeField, Min(0.1f)] private float minimumReactionTime = 0.4f;
    [Tooltip("Extra time to switch between jumping and sliding")]
    [SerializeField, Min(0f)] private float actionChangeBuffer = 0.25f;
    [Tooltip("Extra horizontal clearance for the widths of the player and obstacles")]
    [SerializeField, Min(0f)] private float obstacleClearance = 2f;

    private float distanceCounter;
    private float targetDistance;
    private bool nextIsTop;
    private bool previousWasTop;
    private bool hasPreviousSpike;
    private Camera gameCamera;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerScript>();
        gameCamera = Camera.main;
        UpdateVisibleSpeedLimit();
        SetNextTargetDistance();
    }

    private void Update()
    {
        // Recalculate for resizing and changes in camera framing, including mobile aspect ratios.
        UpdateVisibleSpeedLimit();
    }

    private void FixedUpdate()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing)
            return;

        float speed = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 5f;
        distanceCounter += speed * Time.fixedDeltaTime;

        // Re-evaluate as speed grows; a gap selected at low speed must not stay too short.
        float requiredDistance = Mathf.Max(targetDistance, GetRecoveryDistance(speed));
        if (distanceCounter >= requiredDistance)
        {
            SpawnSpike();
            SetNextTargetDistance();
            // Quantize upwards to one physics step instead of spawning a catch-up burst.
            distanceCounter = 0f;
        }
    }

    private void SpawnSpike()
    {
        SpikeScript prefab = nextIsTop ? topSpikePrefab : bottomSpikePrefab;
        if (prefab == null)
        {
            Debug.LogError("SpikeGenerator: Missing obstacle prefab.", this);
            enabled = false;
            return;
        }

        Vector3 pos = transform.position;
        pos.y = nextIsTop ? topSpikeY : bottomSpikeY;

        SpikeScript spike = null;
        if (PoolManager.Instance != null)
        {
            spike = PoolManager.Instance.Spawn(prefab, pos);
        }
        else
        {
            spike = Instantiate(prefab, pos, Quaternion.identity);
            spike.prefabSource = prefab;
            spike.Initialize();
        }

        if (spike != null)
        {
            previousWasTop = nextIsTop;
            hasPreviousSpike = true;
        }
    }

    private void SetNextTargetDistance()
    {
        nextIsTop = Random.value < chanceForTopSpawn;
        float speedT = 0f;
        if (SpeedManager.Instance != null)
        {
            speedT = SpeedManager.Instance.CurrentNormalized;
        }
        else
        {
            float current = 5f;
            float maxSpeedLocal = 18f;
            speedT = maxSpeedLocal > 0f ? current / maxSpeedLocal : 0f;
        }

        float baseDist = Mathf.Lerp(maxDistance, minDistance, speedT);

        float adjustedRandom = Mathf.Lerp(randomOffset, randomOffset * 0.35f, speedT);
        float offset = Random.Range(-adjustedRandom, adjustedRandom);

        float dist = baseDist + offset;

        float minSafeDistance = Mathf.Lerp(baseMinSafeDistance, maxMinSafeDistance, speedT);

        targetDistance = Mathf.Max(minSafeDistance, dist);
    }

    private float GetRecoveryDistance(float speed)
    {
        if (!hasPreviousSpike) return 0f;

        float jumpTime = player != null ? player.JumpRecoveryTime : 1f;
        float slideTime = player != null ? player.slideDuration : 0.4f;
        float recoveryTime = previousWasTop ? slideTime : jumpTime;
        if (previousWasTop != nextIsTop)
            recoveryTime = Mathf.Max(jumpTime, slideTime);

        return speed * (recoveryTime + actionChangeBuffer) + obstacleClearance;
    }

    private void UpdateVisibleSpeedLimit()
    {
        if (player == null || gameCamera == null || !gameCamera.orthographic || SpeedManager.Instance == null)
            return;

        float depth = Mathf.Abs(player.transform.position.z - gameCamera.transform.position.z);
        float visibleRight = gameCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;
        float visibleDistance = Mathf.Min(transform.position.x, visibleRight) - player.transform.position.x;
        float speedLimit = (visibleDistance - obstacleClearance) / Mathf.Max(0.1f, minimumReactionTime);
        SpeedManager.Instance.SetGameplaySpeedLimit(speedLimit);
    }

    private void OnDisable()
    {
        if (SpeedManager.Instance != null)
            SpeedManager.Instance.SetGameplaySpeedLimit(float.PositiveInfinity);
    }
}
