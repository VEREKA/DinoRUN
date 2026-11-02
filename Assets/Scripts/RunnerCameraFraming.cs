using UnityEngine;

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Camera))]
public class RunnerCameraFraming : MonoBehaviour
{
    public Transform player;
    [Range(0.05f, 0.4f)] public float horizontalAnchor = 0.125f;
    [Min(0f)] public float minimumLeftClearance = 1f;
    private Camera gameCamera;

    private void Awake()
    {
        gameCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        if (player == null)
        {
            var actor = FindFirstObjectByType<PlayerScript>();
            if (actor != null) player = actor.transform;
        }
        ApplyFraming();
    }

    private void Update() => ApplyFraming();

    public void ApplyFraming()
    {
        if (player == null || gameCamera == null || !gameCamera.orthographic) return;
        float width = 2f * gameCamera.orthographicSize * gameCamera.aspect;
        float clearance = Mathf.Max(width * horizontalAnchor, minimumLeftClearance);
        Vector3 position = transform.position;
        position.x = player.position.x + width * 0.5f - clearance;
        transform.position = position;
    }
}
