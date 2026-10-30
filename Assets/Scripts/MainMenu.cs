using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance { get; private set; }

    [Header("UI Buttons")]
    public Button startButton;
    public Button tryAgainButton;
    public Image endScreenImage;
    public TMP_Text livescoreTxt;
    public TMP_Text endscoreTxt;
    public TMP_Text gameOverTxt;
    public TMP_Text pressAnyKeyTxt;
    public GameObject arrows;
    public GameObject instructions;

    private bool waitingForInput = false;
    private float ignoreInputUntil = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (startButton != null) startButton.gameObject.SetActive(true);
        if (tryAgainButton != null) tryAgainButton.gameObject.SetActive(false);
        if (endScreenImage != null) endScreenImage.gameObject.SetActive(false);
        if (livescoreTxt != null) livescoreTxt.gameObject.SetActive(true);
        if (endscoreTxt != null) endscoreTxt.gameObject.SetActive(false);
        if (gameOverTxt != null) gameOverTxt.gameObject.SetActive(false);
        if (arrows != null) arrows.SetActive(false);
        if (instructions != null) instructions.SetActive(false);
        if (pressAnyKeyTxt != null) pressAnyKeyTxt.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!waitingForInput) return;

        if (Time.time < ignoreInputUntil) return;

        if (Input.anyKeyDown || Input.touchCount > 0)
        {
            waitingForInput = false;

            if (pressAnyKeyTxt != null) pressAnyKeyTxt.gameObject.SetActive(false);
            if (arrows != null) arrows.SetActive(false);
            if (instructions != null) instructions.SetActive(false);

            GameStateManager.Instance?.SetState(GameState.Playing);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void TryAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeManagers();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeManagers();
    }

    // Idempotentne: "-=" przed "+=" gwarantuje dokładnie jedną subskrypcję,
    // nawet gdy wywołamy to z OnEnable i z OnSceneLoaded (managery mogą
    // powstać dopiero po załadowaniu pierwszej sceny - ManagerBootstrapper).
    private void SubscribeManagers()
    {
        UnsubscribeManagers();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
    }

    private void UnsubscribeManagers()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GameScene") return;
        waitingForInput = true;
        ignoreInputUntil = Time.time + 0.15f;

        SubscribeManagers();

        GameStateManager.Instance?.SetState(GameState.Menu);

        ScoreManager.Instance?.ResetScore();
        SpeedManager.Instance?.ResetSpeed();

        if (pressAnyKeyTxt != null) pressAnyKeyTxt.gameObject.SetActive(true);
        if (arrows != null) arrows.SetActive(true);
        if (instructions != null) instructions.SetActive(true);

        if (tryAgainButton != null) tryAgainButton.gameObject.SetActive(false);
        if (endScreenImage != null) endScreenImage.gameObject.SetActive(false);
        if (livescoreTxt != null) livescoreTxt.gameObject.SetActive(true);
        if (endscoreTxt != null) endscoreTxt.gameObject.SetActive(false);
        if (gameOverTxt != null) gameOverTxt.gameObject.SetActive(false);
    }

    public void ShowTryAgain()
    {
        if (tryAgainButton != null) tryAgainButton.gameObject.SetActive(true);
        if (endScreenImage != null) endScreenImage.gameObject.SetActive(true);
        if (livescoreTxt != null) livescoreTxt.gameObject.SetActive(false);
        if (endscoreTxt != null) endscoreTxt.gameObject.SetActive(true);
        if (gameOverTxt != null) gameOverTxt.gameObject.SetActive(true);
        if (arrows != null) arrows.SetActive(false);
        if (instructions != null) instructions.SetActive(false);
    }

    public void ShowFinalScore(float score)
    {
        if (endscoreTxt != null)
            endscoreTxt.text = $"GRATULACJE! TWÓJ WYNIK: {score:F0} PKT";
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.GameOver)
        {
            ShowTryAgain();
            ShowFinalScore(ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0f);
        }
    }

    private void HandleScoreChanged(float score)
    {
        if (livescoreTxt != null)
            livescoreTxt.text = $"SCORE: {score:F0}";
    }
}
