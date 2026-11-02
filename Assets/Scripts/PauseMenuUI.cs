using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    private GameStateManager manager;
    private RectTransform root;
    private RectTransform safeArea;
    private RectTransform modalSafeArea;
    private RectTransform card;
    private GameObject overlay;
    private Button pauseButton;
    private Button resumeButton;
    private TMP_FontAsset font;
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;

    private void OnEnable()
    {
        manager = GameStateManager.Instance;
        if (manager != null) manager.OnStateChanged += RefreshState;
        if (root != null && manager != null)
        {
            root.gameObject.SetActive(true);
            RefreshState(manager.State);
        }
    }

    public void Initialize(Canvas canvas, TMP_FontAsset uiFont)
    {
        if (root != null) return;
        font = uiFont;
        root = Rect("PauseUI", canvas.transform);
        Stretch(root);
        safeArea = Rect("SafeArea", root);
        Stretch(safeArea);
        pauseButton = MakeButton("PauseButton", "PAUZA", safeArea);
        var pauseRect = (RectTransform)pauseButton.transform;
        pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1f, 1f);
        pauseRect.pivot = new Vector2(1f, 1f);
        pauseRect.anchoredPosition = new Vector2(-16f, -16f);
        pauseRect.sizeDelta = new Vector2(128f, 48f);
        pauseButton.onClick.AddListener(Pause);

        var dim = Rect("PauseOverlay", root);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 0.88f);
        overlay = dim.gameObject;
        modalSafeArea = Rect("SafeArea", dim);
        Stretch(modalSafeArea);
        card = Rect("PauseCard", modalSafeArea);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(420f, 248f);
        card.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.12f, 0.18f, 1f);
        var border = card.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.28f, 0.65f, 0.75f, 1f);
        border.effectDistance = new Vector2(2f, -2f);

        MakeText("Title", "PAUZA", card, 38f, new Vector2(0f, 65f), new Vector2(360f, 56f));
        MakeText("Hint", "Robot czeka na dalszy bieg.", card, 20f, new Vector2(0f, 16f), new Vector2(360f, 36f));
        resumeButton = MakeButton("ResumeButton", "GRAJ DALEJ", card);
        var resumeRect = (RectTransform)resumeButton.transform;
        resumeRect.anchorMin = resumeRect.anchorMax = resumeRect.pivot = new Vector2(0.5f, 0.5f);
        resumeRect.anchoredPosition = new Vector2(0f, -58f);
        resumeRect.sizeDelta = new Vector2(280f, 56f);
        resumeButton.onClick.AddListener(Resume);
        UpdateSafeArea();
        RefreshState(manager != null ? manager.State : GameState.Menu);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.gameObject.layer = LayerMask.NameToLayer("UI");
        result.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private Button MakeButton(string name, string label, Transform parent)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.42f, 0.53f, 1f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var text = MakeText("Label", label, rect, 24f, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

    private TMP_Text MakeText(string name, string value, Transform parent, float size, Vector2 position, Vector2 dimensions)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.7f;
        text.fontSizeMax = size;
        return text;
    }

    private void LateUpdate()
    {
        if (root == null) return;
        if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2(Screen.width, Screen.height))
            UpdateSafeArea();
    }

    private void UpdateSafeArea()
    {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        Vector2 min = lastSafeArea.position / lastScreenSize;
        Vector2 max = (lastSafeArea.position + lastSafeArea.size) / lastScreenSize;
        foreach (var area in new[] { safeArea, modalSafeArea })
        {
            area.anchorMin = min;
            area.anchorMax = max;
            area.offsetMin = area.offsetMax = Vector2.zero;
        }
        card.sizeDelta = new Vector2(Mathf.Min(420f, root.rect.width * (max.x - min.x) - 32f), 248f);
    }

    private void RefreshState(GameState state)
    {
        if (root == null) return;
        pauseButton.gameObject.SetActive(state == GameState.Playing);
        overlay.SetActive(state == GameState.Paused);
        if (state == GameState.Paused) root.SetAsLastSibling();
    }

    public void Pause()
    {
        if (manager != null && manager.State == GameState.Playing) manager.SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (manager != null && manager.State == GameState.Paused) manager.SetState(GameState.Playing);
    }

    private void OnDisable()
    {
        if (manager != null) manager.OnStateChanged -= RefreshState;
        manager = null;
        if (root != null) root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
