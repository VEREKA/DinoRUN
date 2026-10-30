using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerScript : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Impulse force applied when the player jumps")]
    public float JumpForce = 12f;
    [Tooltip("Duration of slide in seconds")]
    public float slideDuration = 0.4f;

    [Header("Score")]
    [Tooltip("UI text element displaying current score")]
    public TMP_Text ScoreTxt;
    [Tooltip("Multiplier applied to score per second")]
    public float scoreMultiplier = 4f;

    private float score = 0f;
    private float nextScoreUpdate = 0f;

    private bool isGrounded = false;
    private bool isAlive = true;
    private bool JumpRequested = false;
    private bool SlideRequested = false;
    private bool isSliding = false;

    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Vector2 originalColliderSize;
    private Vector2 slideColliderSize;
    private Animator anim;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();

        if (rb == null)
        {
            Debug.LogError("PlayerScript requires a Rigidbody2D component.");
            enabled = false;
            return;
        }

        if (col == null)
            Debug.LogWarning("PlayerScript: CapsuleCollider2D not found; sliding will be disabled.");

        if (anim == null)
            Debug.LogWarning("PlayerScript: Animator not found; animations will not play.");

        if (col != null)
        {
            originalColliderSize = col.size;
            slideColliderSize = new Vector2(col.size.x, col.size.y * 0.65f);
        }

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            score = 0f;
            nextScoreUpdate = Time.time;
            isAlive = true;
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    private void Update()
    {
        if (!isAlive)
            return;

        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing)
            return;

        // ----------------- PC (klawiatura) -----------------
        // Skok: strzałka w górę, W, spacja. Ślizg: strzałka w dół, S.
        if (isGrounded && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)))
            JumpRequested = true;

        if (isGrounded && (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)))
            SlideRequested = true;

        // ----------------- DOTYK -----------------
        // Telefon: górna połowa ekranu = skok, dolna = ślizg.
        // Dotyk sprawdzamy najpierw, bo Unity emuluje z niego też klik myszy.
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began && isGrounded)
            {
                if (touch.position.y > Screen.height * 0.5f)
                    JumpRequested = true;
                else
                    SlideRequested = true;
            }
        }
        // ----------------- MYSZ -----------------
        // Desktop: lewy przycisk = skok, prawy = ślizg (bez dzielenia ekranu).
        else
        {
            if (Input.GetMouseButtonDown(0) && isGrounded)
                JumpRequested = true;

            if (Input.GetMouseButtonDown(1) && isGrounded)
                SlideRequested = true;
        }

        // ----------------- WYNIK -----------------
        // Jedno źródło prawdy: ScoreManager. UI odświeżane co 0,1 s.
        score += Time.deltaTime * scoreMultiplier;

        if (Time.time >= nextScoreUpdate)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.UpdateScore(score);
            else if (ScoreTxt != null)
                ScoreTxt.text = $"SCORE: {score:F0}";
            nextScoreUpdate = Time.time + 0.1f;
        }
    }

    private void FixedUpdate()
    {
        if (JumpRequested)
        {
            JumpRequested = false;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
            isGrounded = false;

            if (isSliding)
                stopSlide();
        }

        if (SlideRequested)
        {
            SlideRequested = false;
            startSlide();
        }
    }

    private void startSlide()
    {
        if (!isGrounded)
            return;

        isSliding = true;
        if (col != null)
            col.size = slideColliderSize;

        if (anim != null)
            anim.SetBool("isSliding", true);

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -4f);
        Invoke(nameof(stopSlide), slideDuration);
    }

    private void stopSlide()
    {
        if (!isSliding)
            return;

        isSliding = false;
        if (col != null)
            col.size = originalColliderSize;

        if (anim != null)
            anim.SetBool("isSliding", false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAlive)
            return;

        if (collision.gameObject.CompareTag("ground"))
        {
            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    isGrounded = true;
                    break;
                }
            }
        }

        if (collision.gameObject.CompareTag("spike"))
            Die();
    }

    private void Die()
    {
        isAlive = false;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        ScoreManager.Instance?.UpdateScore(score);
        GameStateManager.Instance?.SetState(GameState.GameOver);

#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalCall("SetUnityScore", (int)score);
#endif
    }
}
