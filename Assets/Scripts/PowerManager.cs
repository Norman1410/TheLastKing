using UnityEngine;
using UnityEngine.UI;

public class PowerManager : MonoBehaviour
{
    [Header("HUD Power Slots")]
    public Image powerSlot1;    // HUD Image for Power 1
    public Image powerSlot2;    // HUD Image for Power 2
    
    [Header("Power Icons")]
    public Sprite boostPowerIcon;   // Boost power icon
    public Sprite shieldPowerIcon;  // Shield power icon
    public Sprite jumpHighPowerIcon;   // Jump high power icon
    
    private bool[] powerSlots = new bool[2]; // Array to track which slots are occupied
    
    private void Start()
    {
        // At start, clear all power slots
        ClearAllPowerSlots();
    }
    
    private void Update()
    {
        // Detect if R key is pressed to use power
        if (Input.GetKeyDown(KeyCode.R))
        {
            UsePower();
        }
    }
    
    // Method to add a power to the HUD
    public void AddPower(PowerType powerType)
    {
        // Find the first available slot
        for (int i = 0; i < powerSlots.Length; i++)
        {
            if (!powerSlots[i]) // If the slot is empty
            {
                // Mark the slot as occupied
                powerSlots[i] = true;
                
                // Get the corresponding icon
                Sprite powerIcon = GetPowerIcon(powerType);
                
                // Assign the icon to the corresponding slot
                switch (i)
                {
                    case 0:
                        powerSlot1.sprite = powerIcon;
                        powerSlot1.enabled = true;
                        powerSlot1.gameObject.SetActive(true); // Ensure GameObject is active
                        break;
                    case 1:
                        powerSlot2.sprite = powerIcon;
                        powerSlot2.enabled = true;
                        powerSlot2.gameObject.SetActive(true); // Ensure GameObject is active
                        break;
                }
                
                Debug.Log($"Power {powerType} added to slot {i + 1}");
                return; // Exit method once power is added
            }
        }
        
        // If we reach here, no slots are available
        Debug.Log("No available slots for more powers!");
    }
    
    // Method to get the power icon based on its type
    private Sprite GetPowerIcon(PowerType powerType)
    {
        switch (powerType)
        {
            case PowerType.Boost:
                return boostPowerIcon;
            case PowerType.Shield:
                return shieldPowerIcon;
            case PowerType.JumpHigh:
                return jumpHighPowerIcon;
            default:
                return null;
        }
    }
    
    // Método para limpiar todos los slots (opcional, para testing)
    public void ClearAllPowerSlots()
    {
        for (int i = 0; i < powerSlots.Length; i++)
        {
            powerSlots[i] = false;
        }
        
        if (powerSlot1 != null) 
        {
            powerSlot1.enabled = false;
            powerSlot1.sprite = null;
        }
        if (powerSlot2 != null) 
        {
            powerSlot2.enabled = false;
            powerSlot2.sprite = null;
        }
    }
    
    // Method to use power from slot 1 (with R key)
    public void UsePower()
    {
        // Check if there's any power in slot 1
        if (powerSlots[0]) // If slot 1 has a power
        {
            Debug.Log("Power used! (No power logic yet)");
            
            // Shift powers: Slot 2 → Slot 1
            ShiftPowersLeft();
        }
        else
        {
            Debug.Log("No powers to use");
        }
    }
    
    // Method to shift powers to the left
    private void ShiftPowersLeft()
    {
        // If slot 2 has a power, move it to slot 1
        if (powerSlots[1])
        {
            // Move sprite from slot 2 to slot 1
            powerSlot1.sprite = powerSlot2.sprite;
            powerSlot1.enabled = true;
            powerSlot1.gameObject.SetActive(true);
            
            // Clear slot 2
            powerSlot2.sprite = null;
            powerSlot2.enabled = false;
            
            // Update slots array
            powerSlots[0] = true;  // Slot 1 now has power
            powerSlots[1] = false; // Slot 2 is now empty
        }
        else
        {
            // If no power in slot 2, simply clear slot 1
            powerSlot1.sprite = null;
            powerSlot1.enabled = false;
            powerSlots[0] = false;
        }
    }
    
    // Method to check if there are available slots
    public bool HasAvailableSlot()
    {
        for (int i = 0; i < powerSlots.Length; i++)
        {
            if (!powerSlots[i]) return true;
        }
        return false;
    }
}

// Enum for power types
public enum PowerType
{
    Boost,
    Shield,
    JumpHigh
}