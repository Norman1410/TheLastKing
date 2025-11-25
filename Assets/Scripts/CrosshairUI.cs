using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private float size = 20f;
    
    void Start()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();
        
        if (crosshairImage == null)
            crosshairImage = GetComponent<Image>();
        
        // Ensure the image is enabled and warn if no sprite is set (helps debug why it's invisible)
        if (crosshairImage != null)
        {
            crosshairImage.enabled = true;
            if (crosshairImage.sprite == null)
                Debug.LogWarning("Crosshair Image has no sprite assigned. Assign a sprite to see it in-game.", this);
            else if (crosshairImage.color.a <= 0f)
                crosshairImage.color = new Color(crosshairImage.color.r, crosshairImage.color.g, crosshairImage.color.b, 1f);
        }
        
        // Center the crosshair
        if (crosshairRect != null)
        {
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = new Vector2(size, size);
        }
    }
    
    public void SetColor(Color color)
    {
        if (crosshairImage != null)
            crosshairImage.color = color;
    }
    
    public void SetSize(float newSize)
    {
        size = newSize;
        if (crosshairRect != null)
            crosshairRect.sizeDelta = new Vector2(size, size);
    }
}
