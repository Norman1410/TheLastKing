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
    }

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

    public static void Hide()
    {
        if (Instance != null) Instance.InternalHide();
    }

    void InternalHide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
