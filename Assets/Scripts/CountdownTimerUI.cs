using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Muestra un cronómetro en pantalla usando sprites para cada número y el colon.
/// Coloca 5 Image UI: minuteTen, minuteUnit, colonImage, secondTen, secondUnit.
/// Llama StartTimer(seconds) para iniciar. Dispara onTimerEnd cuando llega a 0.
/// </summary>
public class CountdownTimerUI : MonoBehaviour
{
    [Header("Timer")]
    public int durationSeconds = 60;
    public bool startOnAwake = false;

    [Header("Sprites")]
    [Tooltip("Sprites for digits 0..9 in order")]
    public Sprite[] digitSprites = new Sprite[10];
    public Sprite colonSprite;

    [Header("UI Images (assign or use the helper) ")]
    public Image minuteTen;
    public Image minuteUnit;
    public Image colonImage;
    public Image secondTen;
    public Image secondUnit;

    [Header("Events")]
    public UnityEvent onTimerEnd;

    float remaining = 0f;
    int lastDisplayed = -1;

    

    void Awake()
    {
        // Try to auto-load sprites from Resources if not assigned
        TryAutoLoadSpritesFromResources();

        // Try to auto-wire Image references if the designer didn't assign them in the inspector
        TryAutoWireImages();

        if (startOnAwake) StartTimer(durationSeconds);
        // show initial
        UpdateDisplay(durationSeconds);
    }

    void TryAutoLoadSpritesFromResources()
    {
        // If digitSprites are already assigned and complete, nothing to do
        bool needDigits = digitSprites == null || digitSprites.Length < 10;
        if (!needDigits)
        {
            for (int i = 0; i < 10; i++) if (digitSprites[i] == null) { needDigits = true; break; }
        }

        if (!needDigits && colonSprite != null) return;

        // Try common resource names where author might have placed the digits atlas/sprites
        string[] resourceCandidates = new string[] { "Numbers-Poly", "Numbers", "Digits", "TimerNumbers" };
        Sprite[] loaded = null;

        foreach (var candidate in resourceCandidates)
        {
            try
            {
                var all = Resources.LoadAll<Sprite>(candidate);
                if (all != null && all.Length > 0)
                {
                    loaded = all;
                    Debug.Log($"CountdownTimerUI: Loaded {all.Length} sprites from Resources/{candidate}");
                    break;
                }
            }
            catch { }
        }

        if (loaded == null)
        {
            // As a last resort try to load entire Resources folder (not recommended but helpful)
            try { loaded = Resources.LoadAll<Sprite>(""); } catch { loaded = null; }
        }

        if (loaded == null || loaded.Length == 0) return;

        // Create array if needed
        if (digitSprites == null || digitSprites.Length < 10) digitSprites = new Sprite[10];

        // Try to find sprites named 'num0'..'num9' or '0'..'9' or containing those digits
        for (int d = 0; d <= 9; d++)
        {
            if (digitSprites[d] != null) continue;
            string name0 = d.ToString();
            string numName = "num" + d;
            foreach (var s in loaded)
            {
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                var sn = s.name.ToLowerInvariant();
                // Match "num0", "num1", etc. or just "0", "1", etc.
                if (sn.Equals(numName, System.StringComparison.OrdinalIgnoreCase) 
                    || sn.Equals(name0, System.StringComparison.OrdinalIgnoreCase) 
                    || (sn.Contains("num") && sn.Contains(name0))
                    || (sn.Contains("digit") && sn.Contains(name0)))
                {
                    digitSprites[d] = s; 
                    Debug.Log($"CountdownTimerUI: Assigned sprite '{s.name}' to digit {d}");
                    break;
                }
            }
        }

        // If colonSprite missing, try to find sprite named 'colon', 'symbol colon', or ':' or 'dot'
        if (colonSprite == null)
        {
            foreach (var s in loaded)
            {
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                var n = s.name.ToLowerInvariant();
                if (n.Contains("colon") || n.Contains("dot") || n.Contains(":") || n.Contains("sep") || n.Contains("symbol"))
                {
                    colonSprite = s; 
                    Debug.Log($"CountdownTimerUI: Assigned sprite '{s.name}' as colon");
                    break;
                }
            }
        }

        Debug.Log($"CountdownTimerUI: Auto-loaded sprites -> digits present: {CountAssignedDigits()}/10, colon={(colonSprite!=null)}");
    }

    int CountAssignedDigits()
    {
        if (digitSprites == null) return 0;
        int c = 0; for (int i = 0; i < digitSprites.Length && i < 10; i++) if (digitSprites[i] != null) c++; return c;
    }

    /// <summary>
    /// Ensure sprites and image references are available, activate the GameObject and start the timer.
    /// Use this to force the host UI to show immediately when Start is pressed.
    /// </summary>
    public void EnsureAndStart(int seconds)
    {
        TryAutoLoadSpritesFromResources();
        TryAutoWireImages();

        // Ensure colon sprite set to colonImage if available
        if (colonImage != null && colonSprite != null)
        {
            colonImage.sprite = colonSprite;
            colonImage.enabled = true;
        }

        // Ensure each image has a sprite placeholder if digitSprites available
        if (digitSprites != null)
        {
            if (minuteTen != null && minuteTen.sprite == null && digitSprites.Length > 0) minuteTen.sprite = digitSprites[0];
            if (minuteUnit != null && minuteUnit.sprite == null && digitSprites.Length > 1) minuteUnit.sprite = digitSprites[0];
            if (secondTen != null && secondTen.sprite == null && digitSprites.Length > 2) secondTen.sprite = digitSprites[0];
            if (secondUnit != null && secondUnit.sprite == null && digitSprites.Length > 3) secondUnit.sprite = digitSprites[0];
        }

        if (gameObject != null) gameObject.SetActive(true);
        StartTimer(seconds);
    }

    void TryAutoWireImages()
    {
        // If the main Image slots are already assigned, nothing to do
        if (minuteTen != null && minuteUnit != null && colonImage != null && secondTen != null && secondUnit != null)
            return;

        // Search children for Image components and try to match by name
        var imgs = GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img == null || img.gameObject == null) continue;
            var n = img.gameObject.name.ToLowerInvariant();
            if (minuteTen == null && (n.Contains("minutet") || n.Contains("minute_t") || n.Contains("minute10") || n.Contains("minute10"))) minuteTen = img;
            else if (minuteUnit == null && (n.Contains("minute") && (n.Contains("unit") || n.Contains("_u") || n.EndsWith("u") || n.Contains("minutetwo")))) minuteUnit = img;
            else if (colonImage == null && (n.Contains("colon") || n.Contains("colonimage") || n.Contains(":"))) colonImage = img;
            else if (secondTen == null && (n.Contains("secondt") || n.Contains("second_t") || n.Contains("second10") || n.Contains("s_ten"))) secondTen = img;
            else if (secondUnit == null && (n.Contains("second") && (n.Contains("unit") || n.Contains("_u") || n.EndsWith("u") || n.Contains("_one")))) secondUnit = img;
        }

        // As a last resort, assign by index if we found at least 5 images in children
        if ((minuteTen == null || minuteUnit == null || colonImage == null || secondTen == null || secondUnit == null) && imgs.Length >= 5)
        {
            // try to pick five reasonably-ordered images
            if (minuteTen == null) minuteTen = imgs[Mathf.Clamp(0, 0, imgs.Length - 1)];
            if (minuteUnit == null) minuteUnit = imgs[Mathf.Clamp(1, 0, imgs.Length - 1)];
            if (colonImage == null) colonImage = imgs[Mathf.Clamp(2, 0, imgs.Length - 1)];
            if (secondTen == null) secondTen = imgs[Mathf.Clamp(3, 0, imgs.Length - 1)];
            if (secondUnit == null) secondUnit = imgs[Mathf.Clamp(4, 0, imgs.Length - 1)];
        }

        // Log what we wired so the developer can adjust in editor if needed
        Debug.Log($"CountdownTimerUI: Auto-wired Images -> minuteTen={(minuteTen!=null)}, minuteUnit={(minuteUnit!=null)}, colon={(colonImage!=null)}, secondTen={(secondTen!=null)}, secondUnit={(secondUnit!=null)}");
    }

    void Update()
    {
        if (remaining <= 0f) return;
        remaining -= Time.deltaTime;
        int sec = Mathf.CeilToInt(remaining);
        if (sec != lastDisplayed)
        {
            UpdateDisplay(sec);
            lastDisplayed = sec;
        }
        if (remaining <= 0f)
        {
            remaining = 0f;
            UpdateDisplay(0);
            onTimerEnd?.Invoke();
        }
    }

    /// <summary>
    /// Inicia el cronómetro desde segundos dados.
    /// </summary>
    public void StartTimer(int seconds)
    {
        remaining = seconds;
        lastDisplayed = -1;
        UpdateDisplay(seconds);
        enabled = true;
    }

    /// <summary>
    /// Detiene el timer y actualiza la vista.
    /// </summary>
    public void StopTimer()
    {
        remaining = 0f;
        UpdateDisplay(0);
        enabled = false;
    }

    void UpdateDisplay(int totalSeconds)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int mT = (minutes / 10) % 10;
        int mU = minutes % 10;
        int sT = seconds / 10;
        int sU = seconds % 10;

        SetSprite(minuteTen, mT);
        SetSprite(minuteUnit, mU);
        if (colonImage != null)
        {
            if (colonSprite != null)
            {
                colonImage.sprite = colonSprite;
                colonImage.enabled = true;
            }
            else
            {
                colonImage.enabled = false;
            }
        }
        SetSprite(secondTen, sT);
        SetSprite(secondUnit, sU);
    }

    void SetSprite(Image img, int digit)
    {
        if (img == null) return;
        if (digitSprites != null && digitSprites.Length > digit && digitSprites[digit] != null)
        {
            img.sprite = digitSprites[digit];
            img.enabled = true;
        }
        else
        {
            img.enabled = false;
        }
    }

    /// <summary>
    /// Devuelve los segundos restantes (útil para sincronizar red).
    /// </summary>
    public int GetRemainingSeconds()
    {
        return Mathf.CeilToInt(remaining);
    }

    /// <summary>
    /// Create a minimal Timer UI in runtime (Canvas + TimerUI + 5 Images) and return the CountdownTimerUI component.
    /// This is used when no Timer UI exists in the scene and we must show the sprite timer immediately.
    /// </summary>
    public static CountdownTimerUI CreateRuntimeTimerUI()
    {
        // If a CountdownTimerUI already exists anywhere (including inactive), reuse it (avoid duplicates)
        var existingCt = FindAnyIncludingInactive();
        if (existingCt != null) return existingCt;

            // Find or create a Canvas
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            GameObject canvasGO = null;
        if (canvas != null) canvasGO = canvas.gameObject;
        else
        {
            canvasGO = new GameObject("TLK_TimerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // ensure it's on top
            canvas.sortingOrder = 1000;
            var cs = canvasGO.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            Object.DontDestroyOnLoad(canvasGO);
        }

    // Create TimerUI root with HorizontalLayoutGroup for even spacing
    var timerGO = new GameObject("TimerUI", typeof(RectTransform), typeof(CanvasRenderer));
    timerGO.transform.SetParent(canvasGO.transform, false);
    var rt = timerGO.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.anchoredPosition = new Vector2(0, -20);
    rt.sizeDelta = new Vector2(400, 100);
    var h = timerGO.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
    h.childAlignment = TextAnchor.MiddleCenter;
    h.spacing = 6f;
    h.childForceExpandHeight = false;
    h.childForceExpandWidth = false;
    var fitter = timerGO.AddComponent<UnityEngine.UI.ContentSizeFitter>();
    fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        // Add CountdownTimerUI
        var ct = timerGO.AddComponent<CountdownTimerUI>();

        // Create 5 images: minuteTen, minuteUnit, colon, secondTen, secondUnit
        Sprite[] loaded = null;
        try { loaded = Resources.LoadAll<Sprite>("Numbers-Poly"); } catch { loaded = null; }

        for (int i = 0; i < 5; i++)
        {
            string childName = i == 0 ? "MinuteTen" : i == 1 ? "MinuteUnit" : i == 2 ? "Colon" : i == 3 ? "SecondTen" : "SecondUnit";
            var imgGO = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgGO.transform.SetParent(timerGO.transform, false);
            var irt = imgGO.GetComponent<RectTransform>();
            irt.sizeDelta = new Vector2(80, 80);
            // Add LayoutElement to control size within HorizontalLayoutGroup
            var le = imgGO.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = 64;
            le.preferredHeight = 80;
            var img = imgGO.GetComponent<Image>();
            // assign a placeholder sprite if possible
            if (loaded != null && loaded.Length > 0)
            {
                // try to assign num0 as placeholder
                foreach (var s in loaded) { if (s != null && s.name.ToLowerInvariant().Contains("num0")) { img.sprite = s; break; } }
                if (img.sprite == null) img.sprite = loaded[0];
            }

            if (i == 0) ct.minuteTen = img;
            else if (i == 1) ct.minuteUnit = img;
            else if (i == 2) ct.colonImage = img;
            else if (i == 3) ct.secondTen = img;
            else if (i == 4) ct.secondUnit = img;
        }

        // Try to auto-load digit sprites
        ct.TryAutoLoadSpritesFromResources();
        ct.TryAutoWireImages();
        Object.DontDestroyOnLoad(timerGO);
        Debug.Log("CountdownTimerUI: Created runtime TimerUI and wired default images.");
        return ct;
    }

    /// <summary>
    /// Find a CountdownTimerUI either among active objects or among resources (including inactive GameObjects).
    /// </summary>
    public static CountdownTimerUI FindAnyIncludingInactive()
    {
        try
        {
            var active = Object.FindAnyObjectByType<CountdownTimerUI>();
            if (active != null) return active;
        }
        catch { }

        // Search including inactive objects
        try
        {
            var all = Resources.FindObjectsOfTypeAll<CountdownTimerUI>();
            if (all != null && all.Length > 0) return all[0];
        }
        catch { }

        return null;
    }
}
