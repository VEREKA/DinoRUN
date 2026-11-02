using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlayerScript : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Impulse force applied when the player jumps")]
    public float JumpForce = 12f;
    [Tooltip("Duration of slide in seconds")]
    public float slideDuration = 0.4f;
    [Tooltip("How long a jump pressed just before landing is remembered")]
    [Min(0f)] public float jumpBufferDuration = 0.12f;

    [Header("Score")]
    [Tooltip("Multiplier applied to score per second")]
    public float scoreMultiplier = 4f;

    private bool isGrounded = false;
    private bool isAlive = true;
    private bool JumpRequested = false;
    private bool SlideRequested = false;
    private bool isSliding = false;

    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Vector2 originalColliderSize;
    private Vector2 slideColliderSize;
    private Vector2 originalColliderOffset;
    private float slideTimeRemaining;
    private Animator anim;
    private GameStateManager stateManager;
    private GameState previousState;
    private float jumpBufferRemaining;
    private readonly HashSet<Collider2D> groundContacts = new HashSet<Collider2D>();
    private PlayerSlideVisuals visuals;

    // Time until a jump can be followed by another grounded action.
    public float JumpRecoveryTime => rb != null && Physics2D.gravity.y * rb.gravityScale < 0f
        ? 2f * JumpForce / (rb.mass * -Physics2D.gravity.y * rb.gravityScale)
        : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        anim = GetComponentInChildren<Animator>();
        visuals = GetComponentInChildren<PlayerSlideVisuals>();

        if (rb == null)
        {
            Debug.LogError("PlayerScript requires a Rigidbody2D component.");
            enabled = false;
            return;
        }

        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (col == null)
            Debug.LogWarning("PlayerScript: CapsuleCollider2D not found; sliding will be disabled.");

        if (anim == null)
            Debug.LogWarning("PlayerScript: Animator not found; animations will not play.");

        if (col != null)
        {
            originalColliderSize = col.size;
            originalColliderOffset = col.offset;
            slideColliderSize = new Vector2(col.size.x, col.size.y * 0.65f);
        }

    }

    private void OnEnable()
    {
        stateManager = GameStateManager.Instance;
        previousState = stateManager != null ? stateManager.State : GameState.Menu;
        if (stateManager != null)
            stateManager.OnStateChanged += HandleStateChanged;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ScorePerSecond = scoreMultiplier;
    }

    private void OnDisable()
    {
        if (stateManager != null)
            stateManager.OnStateChanged -= HandleStateChanged;
        stateManager = null;
        JumpRequested = false;
        jumpBufferRemaining = 0f;
        SlideRequested = false;
        groundContacts.Clear();
        isGrounded = false;
        stopSlide();
    }

    private void HandleStateChanged(GameState state)
    {
        bool resuming = previousState == GameState.Paused;
        previousState = state;
        if (state != GameState.Playing)
        {
            JumpRequested = false;
            jumpBufferRemaining = 0f;
            SlideRequested = false;
        }
        if (state == GameState.Playing && !resuming)
        {
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
        if (Input.GetKeyDown(KeyCode.UpArrow))
            RequestJump();

        if (Input.GetKeyDown(KeyCode.DownArrow) && isGrounded)
            SlideRequested = true;

        // ----------------- DOTYK + MYSZ -----------------

        bool inputDown = false;
        Vector2 inputPos = Vector2.zero;

        // ------------------ Mysz ----------------------
        if (Input.GetMouseButtonDown(0) && Input.touchCount == 0 &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            inputDown = true;
            inputPos = Input.mousePosition;
        }

        // ------------------ Dotyk ---------------------
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touch.fingerId)))
            {
                inputDown = true;
                inputPos = touch.position;
            }
        }

        // --------------- Podzia� ekranu ----------------
        if (inputDown)
        {
            float halfScreen = Screen.height * 0.5f;

            if (inputPos.y > halfScreen)
            {
                RequestJump();
            }
            else
            {
                if (isGrounded)
                    SlideRequested = true;
            }
        }

    }

    public void RequestJump()
    {
        if (!isAlive || (stateManager != null && stateManager.State != GameState.Playing)) return;
        JumpRequested = true;
        jumpBufferRemaining = jumpBufferDuration;
    }

    private void FixedUpdate()
    {
        if (!isAlive || (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing))
        {
            JumpRequested = false;
            jumpBufferRemaining = 0f;
            SlideRequested = false;
            return;
        }

        if (JumpRequested && isGrounded)
        {
            JumpRequested = false;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
            isGrounded = false;
            groundContacts.Clear();
            jumpBufferRemaining = 0f;

            if (isSliding)
                stopSlide();
        }

        if (JumpRequested)
        {
            jumpBufferRemaining -= Time.fixedDeltaTime;
            if (jumpBufferRemaining <= 0f) JumpRequested = false;
        }

        if (isSliding)
        {
            slideTimeRemaining -= Time.fixedDeltaTime;
            if (slideTimeRemaining <= 0f) stopSlide();
        }

        if (SlideRequested)
        {
            SlideRequested = false;
            startSlide();
        }
    }

    private void startSlide()
    {
        if (!isGrounded || isSliding || col == null)
            return;

        isSliding = true;
        slideTimeRemaining = Mathf.Max(0f, slideDuration);
        col.size = slideColliderSize;
        // Keep the bottom of the collider in place when crouching.
        col.offset = originalColliderOffset + Vector2.down * (originalColliderSize.y - slideColliderSize.y) * 0.5f;

        if (anim != null)
            anim.SetBool("isSliding", true);
        if (visuals != null) visuals.SetSliding(true);

    }

    private void stopSlide()
    {
        if (!isSliding)
            return;

        isSliding = false;
        slideTimeRemaining = 0f;
        if (col != null)
        {
            col.size = originalColliderSize;
            col.offset = originalColliderOffset;
        }

        if (anim != null)
            anim.SetBool("isSliding", false);
        if (visuals != null) visuals.SetSliding(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAlive)
            return;

        UpdateGroundContact(collision);

        if (collision.gameObject.CompareTag("spike"))
            Die();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isAlive) UpdateGroundContact(collision);
    }

    private void UpdateGroundContact(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("ground")) return;
        bool supporting = false;
        if (rb.linearVelocity.y <= 0.1f)
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y > 0.5f) { supporting = true; break; }
        }
        if (supporting) groundContacts.Add(collision.collider);
        else groundContacts.Remove(collision.collider);
        isGrounded = groundContacts.Count > 0;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        groundContacts.Remove(collision.collider);
        isGrounded = groundContacts.Count > 0;
    }

    private void Die()
    {
        isAlive = false;
        stopSlide();
        JumpRequested = false;
        jumpBufferRemaining = 0f;
        SlideRequested = false;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GameStateManager.Instance?.SetState(GameState.GameOver);

#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalCall("SetUnityScore", (int)(ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0f));
#endif
    }
}
