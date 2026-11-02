using UnityEngine;

public class GroundTile : MonoBehaviour
{
    public float tileWidth = 30.09375f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private GroundTileDebugState debugState;

    private void Awake()
    {
        debugState = GetComponent<GroundTileDebugState>();
    }
#endif

    private void Update()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing)
            return;

        float speed = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 5f;
        transform.Translate(Vector2.left * speed * Time.deltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugState != null && debugState.enableLogs) debugState.MaybeLog(speed, name);
#endif

        if (transform.position.x < -tileWidth)
        {
            transform.position += new Vector3(tileWidth * 2f, 0f, 0f);
        }
    }
}
