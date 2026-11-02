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
    private GameStateManager stateManager;
    private ScoreManager scoreManager;

    private void Start()
    {
        if (Instance == this && livescoreTxt != null)
            gameObject.AddComponent<PauseMenuUI>().Initialize(livescoreTxt.canvas, livescoreTxt.font);
    }

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
        if (Input.GetKeyDown(KeyCode.Escape))
            GameStateManager.Instance?.TogglePause();
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
        if (Instance != this) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        stateManager = GameStateManager.Instance;
        scoreManager = ScoreManager.Instance;
        if (stateManager != null)
            stateManager.OnStateChanged += HandleStateChanged;

        if (scoreManager != null)
        {
            scoreManager.OnScoreChanged += HandleScoreChanged;
            HandleScoreChanged(scoreManager.CurrentScore);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (stateManager != null)
            stateManager.OnStateChanged -= HandleStateChanged;

        if (scoreManager != null)
            scoreManager.OnScoreChanged -= HandleScoreChanged;
        stateManager = null;
        scoreManager = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        GameStateManager.Instance?.SetState(GameState.Menu);
        if (scene.name != "GameScene") return;
        waitingForInput = true;
        ignoreInputUntil = Time.time + 0.15f;

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
        if (state == GameState.Playing)
        {
            waitingForInput = false;
            if (pressAnyKeyTxt != null) pressAnyKeyTxt.gameObject.SetActive(false);
            if (arrows != null) arrows.SetActive(false);
            if (instructions != null) instructions.SetActive(false);
        }
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
