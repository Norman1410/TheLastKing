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
        
        // Centrar el crosshair
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