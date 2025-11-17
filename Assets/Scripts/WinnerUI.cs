using UnityEngine;
using UnityEngine.UI;

// Simple runtime UI to show match winner and match over messages.
public class WinnerUI : MonoBehaviour
{
    public static WinnerUI Instance { get; private set; }

    Canvas canvas;
    GameObject panel;
    Text mainText;
    Text subText;
    Image resultImage;

    [Header("Assign sprites manually in the Inspector")]
    [Tooltip("Sprite to show for the local winner (drag Assets/Images/winner.png here)")]
    public Sprite winnerSprite;

    [Tooltip("Sprite to show for eliminated players (drag Assets/Images/game over.png here)")]
    public Sprite gameOverSprite;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateUI();
        Hide();
    }

    void CreateUI()
    {
        // Canvas
        var go = new GameObject("WinnerUI_Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        // Panel
        panel = new GameObject("Panel");
        panel.transform.SetParent(go.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Main Text
        var mtGO = new GameObject("MainText");
        mtGO.transform.SetParent(panel.transform, false);
        mainText = mtGO.AddComponent<Text>();
        mainText.alignment = TextAnchor.MiddleCenter;
    mainText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mainText.fontSize = 72;
        mainText.color = Color.yellow;
        var mtRect = mainText.GetComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0.1f, 0.55f);
        mtRect.anchorMax = new Vector2(0.9f, 0.9f);
        mtRect.offsetMin = Vector2.zero;
        mtRect.offsetMax = Vector2.zero;

        // Sub Text
        var stGO = new GameObject("SubText");
        stGO.transform.SetParent(panel.transform, false);
        subText = stGO.AddComponent<Text>();
        subText.alignment = TextAnchor.MiddleCenter;
    subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subText.fontSize = 28;
        subText.color = Color.white;
        var stRect = subText.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.1f, 0.2f);
        stRect.anchorMax = new Vector2(0.9f, 0.5f);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // Result Image (centered) - used if sprite files are available
        var riGO = new GameObject("ResultImage");
        riGO.transform.SetParent(panel.transform, false);
        resultImage = riGO.AddComponent<Image>();
        resultImage.preserveAspect = true;
        var riRect = resultImage.GetComponent<RectTransform>();
        // Centered box occupying middle of screen
        riRect.anchorMin = new Vector2(0.25f, 0.25f);
        riRect.anchorMax = new Vector2(0.75f, 0.75f);
        riRect.offsetMin = Vector2.zero;
        riRect.offsetMax = Vector2.zero;

        // Sprites are expected to be assigned manually in the Inspector. No automatic loading performed.

        // Configure Image component for reliable display
        resultImage.type = Image.Type.Simple;
        resultImage.preserveAspect = true;
        resultImage.color = Color.white;

        // If we have a sprite, hide text by default (we'll show appropriate image when triggered)
        if (winnerSprite != null || gameOverSprite != null)
        {
            mainText.gameObject.SetActive(false);
            subText.gameObject.SetActive(false);
            resultImage.gameObject.SetActive(false); // will be enabled when showing
        }
    }

    // No automatic sprite loaders - sprites must be assigned in the Inspector for predictable behavior in builds.

    public static void Show(string winnerName, bool isLocal)
    {
        if (Instance == null)
        {
            var go = new GameObject("WinnerUI");
            Instance = go.AddComponent<WinnerUI>();
        }
        Instance.InternalShow(winnerName, isLocal);
    }

    void InternalShow(string winnerName, bool isLocal)
    {
        if (panel == null) CreateUI();
        panel.SetActive(true);
        // If sprites are available, prefer showing images. Otherwise fall back to text messages.
        if (resultImage != null && (winnerSprite != null || gameOverSprite != null))
        {
            // hide text
            if (mainText != null) mainText.gameObject.SetActive(false);
            if (subText != null) subText.gameObject.SetActive(false);

            resultImage.gameObject.SetActive(true);
            if (isLocal)
            {
                resultImage.sprite = winnerSprite ?? gameOverSprite;
            }
            else
            {
                resultImage.sprite = gameOverSprite ?? winnerSprite;
            }
        }
        else
        {
            if (mainText != null) mainText.gameObject.SetActive(true);
            if (subText != null) subText.gameObject.SetActive(true);
            if (isLocal)
            {
                mainText.text = "YOU ARE THE WINNER!";
                subText.text = "Congratulations!";
            }
            else
            {
                mainText.text = "MATCH OVER";
                subText.text = $"Winner: {winnerName}";
            }
        }
    }

    public static void Hide()
    {
        if (Instance != null) Instance.InternalHide();
    }

    void InternalHide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
