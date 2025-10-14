using UnityEngine;
using UnityEngine.UI;

// Simple HUD helper to expose slot images and update them visually
[DisallowMultipleComponent]
public class PowersHUD : MonoBehaviour
{
    public Image slot1Image;
    public Image slot2Image;

    // Sets the sprite for a slot index (0 or 1). Pass null to clear.
    public void SetSlotSprite(int slotIndex, Sprite sprite)
    {
        if (slotIndex == 0 && slot1Image != null)
        {
            slot1Image.sprite = sprite;
            slot1Image.enabled = sprite != null;
            if (slot1Image.gameObject != null) slot1Image.gameObject.SetActive(sprite != null);
        }
        else if (slotIndex == 1 && slot2Image != null)
        {
            slot2Image.sprite = sprite;
            slot2Image.enabled = sprite != null;
            if (slot2Image.gameObject != null) slot2Image.gameObject.SetActive(sprite != null);
        }
    }
}
