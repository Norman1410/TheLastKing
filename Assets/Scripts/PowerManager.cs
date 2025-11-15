using UnityEngine;
using UnityEngine.UI;

public class PowerManager : MonoBehaviour
{
    // Singleton instance for easy access from PowerUp pickups
    public static PowerManager Instance { get; private set; }

    private PowerType?[] powerTypeSlots = new PowerType?[2]; // stores which PowerType is in each slot
    [Header("HUD Power Slots")]
    public PowersHUD powersHUD; // Reference to the HUD helper component

    [Header("Input")]
    public KeyCode useKey = KeyCode.R; // Key to use the current power (default R)

    [System.Serializable]
    public struct PrefabIconMapping
    {
        public GameObject prefab; // power prefab to match
        public Sprite icon;       // icon to display when this prefab is picked
        public PowerType powerType; // which PowerType this mapping represents
    }

    [Header("Prefab -> Icon mappings (required)")]
    public PrefabIconMapping[] prefabIconMappings; // explicit mappings (prefab->icon + powerType)
    
    private bool[] powerSlots = new bool[2]; // Array to track which slots are occupied
    // Simple dedupe: remember recently picked prefab instance IDs to avoid double-processing
    private System.Collections.Generic.Dictionary<int, float> recentlyPicked = new System.Collections.Generic.Dictionary<int, float>();
    private float dedupeWindow = 1.0f; // seconds within which repeats are ignored
    
    private void Start()
    {
        // Initialize singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // At start, clear all power slots
        ClearAllPowerSlots();

        // No auto-collector: player pickup forwarding is handled externally or by adding PlayerPowerCollector manually.
    }
    
    private void Update()
    {
        // Detect if configured key is pressed to use power
        if (Input.GetKeyDown(useKey))
        {
            UsePower();
        }
    }
    
    // Method to add a power to the HUD
    // Adds a power to the first available slot. Returns true on success.
    // Adds a power to the HUD. powerType can be null: if so we try to infer it from pickedPrefab (mapping or component).
    public bool AddPower(PowerType? powerType = null, GameObject pickedPrefab = null)
    {
        // Dedupe logic: remove stale entries
        var now = Time.time;
        var keysToRemove = new System.Collections.Generic.List<int>();
        foreach (var kv in recentlyPicked)
        {
            if (now - kv.Value > dedupeWindow) keysToRemove.Add(kv.Key);
        }
        foreach (var k in keysToRemove) recentlyPicked.Remove(k);

        // If we have a pickedPrefab, ignore repeats within dedupeWindow
        if (pickedPrefab != null)
        {
            int id = pickedPrefab.GetInstanceID();
            if (recentlyPicked.ContainsKey(id))
            {
                Debug.Log($"PowerManager: ignored duplicate AddPower for {pickedPrefab.name} (id {id})");
                return false;
            }
            else
            {
                recentlyPicked[id] = now;
            }
        }

        // Find the first available slot
        for (int i = 0; i < powerSlots.Length; i++)
        {
            if (!powerSlots[i]) // If the slot is empty
            {
                // Mark the slot as occupied
                powerSlots[i] = true;
                // If prefab provided, try to map to a specific PowerType and icon
                Sprite powerIcon = null;
                if (pickedPrefab != null)
                {
                    // Try mapping by prefab name (handle instantiated names like "Foo(Clone)")
                    string pickedBase = pickedPrefab.name.Replace("(Clone)", "").Trim();
                    foreach (var m in prefabIconMappings)
                    {
                        if (m.prefab != null && m.prefab.name == pickedBase)
                        {
                            powerType = m.powerType;
                            powerIcon = m.icon;
                            break;
                        }
                    }

                    // Fallback: try to infer by component type on the picked instance
                    if (powerType == null)
                    {
                        if (pickedPrefab.GetComponent<Invisibility>() != null) powerType = PowerType.Invisibility;
                        else if (pickedPrefab.GetComponent<Dwarf>() != null) powerType = PowerType.MegaSize;
                        else if (pickedPrefab.GetComponent<SuperJump>() != null) powerType = PowerType.JumpHigh;
                        else if (pickedPrefab.GetComponent<TurboSprint>() != null) powerType = PowerType.Boost;
                        else if (pickedPrefab.GetComponent<Shield>() != null) powerType = PowerType.Shield;
                    }
                }

                // If still no icon from mapping, get default by powerType
                if (powerIcon == null) powerIcon = GetPowerIcon(powerType, pickedPrefab);

                powerTypeSlots[i] = powerType;
                
                // Assign the icon to the corresponding slot
                // Update HUD via helper
                if (powersHUD != null)
                {
                    powersHUD.SetSlotSprite(i, powerIcon);
                }
                
                Debug.Log($"Power {powerType} added to slot {i + 1}");
                return true; // Exit method once power is added
            }
        }
        
        // If we reach here, no slots are available
        Debug.Log("No available slots for more powers!");
        return false;
    }
    
    // Method to get the power icon based on its type
    private Sprite GetPowerIcon(PowerType powerType, GameObject pickedPrefab = null)
    {
        // If prefab specified, try to find exact mapping
        if (pickedPrefab != null && prefabIconMappings != null)
        {
            string pickedBase = pickedPrefab.name.Replace("(Clone)", "").Trim();
            foreach (var m in prefabIconMappings)
            {
                if (m.prefab != null && m.prefab.name == pickedBase)
                {
                    return m.icon;
                }
            }
        }

        // If no prefab mapping matched, try to find a mapping by powerType
        if (prefabIconMappings != null)
        {
            foreach (var m in prefabIconMappings)
            {
                if (m.icon != null && m.powerType == powerType)
                    return m.icon;
            }
        }

        return null;
    }

    // Overload for nullable PowerType
    private Sprite GetPowerIcon(PowerType? powerType, GameObject pickedPrefab = null)
    {
        if (!powerType.HasValue) return null;
        return GetPowerIcon(powerType.Value, pickedPrefab);
    }
    
    // Método para limpiar todos los slots (opcional, para testing)
    public void ClearAllPowerSlots()
    {
        for (int i = 0; i < powerSlots.Length; i++)
        {
            powerSlots[i] = false;
            powerTypeSlots[i] = null;
        }
        
        if (powersHUD != null)
        {
            powersHUD.SetSlotSprite(0, null);
            powersHUD.SetSlotSprite(1, null);
        }
    }
    
    // Method to use power from slot 1 (with R key)
    public void UsePower()
    {
        // Check if there's any power in slot 1
        if (powerSlots[0]) // If slot 1 has a power
        {
            PowerType? type = powerTypeSlots[0];
            if (type.HasValue)
            {
                Debug.Log($"Using power {type.Value}");
                StartCoroutine(HandlePowerEffect(type.Value));
            }

            // Shift powers: Slot 2 → Slot 1
            ShiftPowersLeft();
        }
        else
        {
            Debug.Log("No powers to use");
        }
    }

    // Coroutine that applies the effect for the chosen power type
    private System.Collections.IEnumerator HandlePowerEffect(PowerType type)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("No player found to apply power effect.");
            yield break;
        }

        switch (type)
        {
            case PowerType.Boost:
                // Temporary speed boost on PlayerMovement if present
                var pm = player.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    float original = pm.speed;
                    pm.speed *= 2f;
                    yield return new WaitForSeconds(5f);
                    pm.speed = original;
                }
                break;
            case PowerType.JumpHigh:
                var pc = player.GetComponent<PlayerMovement>();
                if (pc != null)
                {
                    float origJ = pc.jumpHeight;
                    pc.jumpHeight *= 2.5f;
                    yield return new WaitForSeconds(5f);
                    pc.jumpHeight = origJ;
                }
                break;
            case PowerType.Shield:
                // Implement a basic visual shield if player has a Shield component or create a simple invulnerability flag
                var shield = player.GetComponent<Shield>();
                if (shield != null)
                {
                    // If Shield component had logic, call it; otherwise, just wait as placeholder
                }
                yield return new WaitForSeconds(5f);
                break;
            case PowerType.Invisibility:
                // Hide renderers
                var rends = player.GetComponentsInChildren<Renderer>();
                foreach (var r in rends) r.enabled = false;
                yield return new WaitForSeconds(6f);
                foreach (var r in rends) r.enabled = true;
                break;
            case PowerType.MegaSize:
                Vector3 origScale = player.transform.localScale;
                player.transform.localScale = origScale * 2.5f;
                yield return new WaitForSeconds(6f);
                player.transform.localScale = origScale;
                break;
        }

        yield break;
    }
    
    // Method to shift powers to the left
    private void ShiftPowersLeft()
    {
        // If slot 2 has a power, move it to slot 1
        if (powerSlots[1])
        {
            // Move the type from slot 2 to slot 1
            powerTypeSlots[0] = powerTypeSlots[1];
            powerTypeSlots[1] = null;

            // Update HUD sprites
            if (powersHUD != null && powerTypeSlots[0].HasValue)
            {
                Sprite icon = GetPowerIcon(powerTypeSlots[0].Value);
                powersHUD.SetSlotSprite(0, icon);
                powersHUD.SetSlotSprite(1, null);
            }

            // Update slots array
            powerSlots[0] = true;  // Slot 1 now has power
            powerSlots[1] = false; // Slot 2 is now empty
        }
        else
        {
            // If no power in slot 2, simply clear slot 1
            powerTypeSlots[0] = null;
            if (powersHUD != null) powersHUD.SetSlotSprite(0, null);
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
    JumpHigh,
    Invisibility,
    MegaSize
}

// PlayerPowerCollector moved to its own file (Assets/Scripts/PlayerPowerCollector.cs)