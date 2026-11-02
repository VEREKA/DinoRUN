using UnityEngine;

// This foot pivot and its artwork are children of the physics object.
public class PlayerSlideVisuals : MonoBehaviour
{
    [Range(0.4f, 1f)] public float slideHeight = 0.65f;
    [Range(-15f, 0f)] public float slideTilt = -7f;
    [Min(0.01f)] public float transitionDuration = 0.05f;
    public Sprite sparkSprite;

    private const int SparkCount = 6;
    private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount];
    private readonly Vector3[] sparkPositions = new Vector3[SparkCount];
    private readonly Vector3[] sparkVelocities = new Vector3[SparkCount];
    private readonly float[] sparkLife = new float[SparkCount];
    private bool sliding;
    private float blend;
    private float emissionRemaining;
    private int nextSpark;
    private Animator animator;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        var artwork = GetComponentInChildren<SpriteRenderer>();
        if (sparkSprite == null || artwork == null) return;
        for (int i = 0; i < SparkCount; i++)
        {
            var spark = new GameObject("SlideSpark");
            spark.transform.SetParent(transform.parent, false);
            var renderer = spark.AddComponent<SpriteRenderer>();
            renderer.sprite = sparkSprite;
            renderer.sharedMaterial = artwork.sharedMaterial;
            renderer.sortingLayerID = artwork.sortingLayerID;
            renderer.sortingOrder = artwork.sortingOrder + 1;
            spark.transform.localScale = new Vector3(0.08f / sparkSprite.bounds.size.x, 0.025f / sparkSprite.bounds.size.y, 1f);
            spark.SetActive(false);
            sparks[i] = renderer;
        }
    }

    public void SetSliding(bool value)
    {
        if (sliding == value) return;
        sliding = value;
        emissionRemaining = 0f;
        if (animator != null)
        {
            animator.SetBool("isSliding", value);
            // Match the pose to the hitbox immediately; the pivot supplies the short visual easing.
            if (animator.isActiveAndEnabled)
            {
                animator.Play(value ? "Slide" : "Run", 0, 0f);
                animator.Update(0f);
            }
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        blend = Mathf.MoveTowards(blend, sliding ? 1f : 0f, dt / Mathf.Max(0.01f, transitionDuration));
        transform.localScale = new Vector3(1f, Mathf.Lerp(1f, slideHeight, blend), 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, slideTilt * blend);

        for (int i = 0; i < SparkCount; i++)
        {
            if (sparks[i] == null || sparkLife[i] <= 0f) continue;
            sparkLife[i] -= dt;
            sparkVelocities[i] += Vector3.down * (7f * dt);
            sparkPositions[i] += sparkVelocities[i] * dt;
            sparks[i].transform.position = sparkPositions[i];
            sparks[i].color = new Color(1f, 0.72f, 0.25f, Mathf.Clamp01(sparkLife[i] / 0.16f));
            if (sparkLife[i] <= 0f) sparks[i].gameObject.SetActive(false);
        }
        if (!sliding || dt <= 0f || sparks[0] == null) return;
        emissionRemaining -= dt;
        if (emissionRemaining > 0f) return;
        emissionRemaining = 0.035f;
        int index = nextSpark++ % SparkCount;
        sparkPositions[index] = transform.position + Vector3.up * 0.03f;
        float speed = SpeedManager.Instance != null ? SpeedManager.Instance.CurrentSpeed : 5f;
        sparkVelocities[index] = new Vector3(-Mathf.Min(speed * 0.3f, 7f), Random.Range(0.5f, 1.5f), 0f);
        sparkLife[index] = 0.16f;
        sparks[index].transform.position = sparkPositions[index];
        sparks[index].transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        sparks[index].gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        sliding = false;
        blend = 0f;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        for (int i = 0; i < SparkCount; i++)
        {
            sparkLife[i] = 0f;
            if (sparks[i] != null) sparks[i].gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        foreach (var spark in sparks)
            if (spark != null) Destroy(spark.gameObject);
    }
}
