using UnityEngine;
using UnityEngine.UI;

public class PowerManager : MonoBehaviour
{
    // Singleton por proceso (host o cliente)
    public static PowerManager Instance { get; private set; }

    // Referencia al controlador del jugador LOCAL de este proceso
    private FirstPersonController localController;

    private PowerType?[] powerTypeSlots = new PowerType?[2]; // Tipo de poder en cada slot

    [Header("HUD Power Slots")]
    public PowersHUD powersHUD; // Referencia al PowersHUD del prefab

    [Header("Input")]
    public KeyCode useKey = KeyCode.R; // Tecla para usar poder

    [System.Serializable]
    public struct PrefabIconMapping
    {
        public GameObject prefab;    // Prefab del power-up
        public Sprite icon;          // Icono a mostrar
        public PowerType powerType;  // Tipo de poder
    }

    [Header("Prefab -> Icon mappings (required)")]
    public PrefabIconMapping[] prefabIconMappings;

    [Header("Cooldown Management")]
    public float powerCooldownDuration = 1f;
    private float nextPowerUseTime = 0f;
    private bool isPowerEffectActive = false;

    private bool[] powerSlots = new bool[2];

    // Dedupe para evitar doble AddPower por el mismo prefab
    private System.Collections.Generic.Dictionary<int, float> recentlyPicked =
        new System.Collections.Generic.Dictionary<int, float>();
    private float dedupeWindow = 1.0f;

    // =========================================================
    // INICIALIZACIÓN DESDE EL PLAYER LOCAL
    // =========================================================

    /// <summary>
    /// Llamado por FirstPersonController del jugador LOCAL en OnNetworkSpawn.
    /// </summary>
    public void InitializeForLocalPlayer(FirstPersonController controller)
    {
        // Si ya había un Instance, lo reemplazamos (solo debería existir uno por proceso)
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        localController = controller;

        ClearAllPowerSlots();

        Debug.Log("[PowerManager] Inicializado para jugador local: " + controller.gameObject.name);
    }

    private void Update()
    {
        // Si este PowerManager no es el Instance (p.ej. jugador remoto), no hace nada
        if (Instance != this) return;

        if (Input.GetKeyDown(useKey))
        {
            UsePower();
        }
    }

    // =========================================================
    // GESTIÓN DE SLOTS / HUD
    // =========================================================

    // Añadir poder a un slot
    public bool AddPower(PowerType? powerType = null, GameObject pickedPrefab = null)
    {
        // Limpia entradas viejas en el dedupe
        var now = Time.time;
        var keysToRemove = new System.Collections.Generic.List<int>();
        foreach (var kv in recentlyPicked)
        {
            if (now - kv.Value > dedupeWindow) keysToRemove.Add(kv.Key);
        }
        foreach (var k in keysToRemove) recentlyPicked.Remove(k);

        // Evita doble AddPower por el mismo prefab en un tiempo muy corto
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

        // Buscar primer slot libre
        for (int i = 0; i < powerSlots.Length; i++)
        {
            if (!powerSlots[i])
            {
                powerSlots[i] = true;

                // Intentar deducir tipo e icono a partir del prefab
                Sprite powerIcon = null;
                if (pickedPrefab != null)
                {
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

                    // Fallback por componente
                    if (powerType == null)
                    {
                        if (pickedPrefab.GetComponent<Invisibility>() != null) powerType = PowerType.Invisibility;
                        else if (pickedPrefab.GetComponent<Dwarf>() != null) powerType = PowerType.Dwarf;
                        else if (pickedPrefab.GetComponent<SuperJump>() != null) powerType = PowerType.JumpHigh;
                        else if (pickedPrefab.GetComponent<TurboSprint>() != null) powerType = PowerType.Boost;
                        else if (pickedPrefab.GetComponent<Levitate>() != null) powerType = PowerType.Levitate;
                    }
                }

                if (powerIcon == null) powerIcon = GetPowerIcon(powerType, pickedPrefab);

                powerTypeSlots[i] = powerType;

                // Actualizar HUD
                if (powersHUD != null)
                {
                    powersHUD.SetSlotSprite(i, powerIcon);
                }

                Debug.Log($"Power {powerType} added to slot {i + 1}");
                return true;
            }
        }

        Debug.Log("No available slots for more powers!");
        return false;
    }

    private Sprite GetPowerIcon(PowerType powerType, GameObject pickedPrefab = null)
    {
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

    private Sprite GetPowerIcon(PowerType? powerType, GameObject pickedPrefab = null)
    {
        if (!powerType.HasValue) return null;
        return GetPowerIcon(powerType.Value, pickedPrefab);
    }

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

    // =========================================================
    // USO DEL PODER
    // =========================================================

    public void UsePower()
    {
        if (isPowerEffectActive)
        {
            Debug.Log("No puedes usar otro poder hasta que el efecto actual termine.");
            return;
        }

        if (Time.time < nextPowerUseTime)
        {
            Debug.Log($"Poder en cooldown. Espera {(nextPowerUseTime - Time.time):F1} segundos.");
            return;
        }

        if (powerSlots[0])
        {
            PowerType? type = powerTypeSlots[0];
            if (type.HasValue)
            {
                isPowerEffectActive = true;
                Debug.Log($"Using power {type.Value}");
                StartCoroutine(HandlePowerEffect(type.Value));
                nextPowerUseTime = Time.time + powerCooldownDuration;
            }

            ShiftPowersLeft();
        }
        else
        {
            Debug.Log("No powers to use");
        }
    }

    private System.Collections.IEnumerator HandlePowerEffect(PowerType type)
    {
        if (localController == null)
        {
            Debug.LogWarning("PowerManager: localController es null, no puedo aplicar el poder.");
            isPowerEffectActive = false;
            yield break;
        }

        GameObject player = localController.gameObject;
        float duration = 0f;

        switch (type)
        {
            case PowerType.Boost:
                duration = 5f;
                {
                    float originalWalkSpeed = localController.walkSpeed;
                    float originalRunSpeed = localController.runSpeed;

                    localController.walkSpeed *= 3f;
                    localController.runSpeed *= 3f;

                    yield return new WaitForSeconds(duration);

                    localController.walkSpeed = originalWalkSpeed;
                    localController.runSpeed = originalRunSpeed;
                }
                break;

            case PowerType.JumpHigh:
                duration = 5f;
                {
                    float origJ = localController.jumpHeight;
                    localController.jumpHeight *= 4f;

                    yield return new WaitForSeconds(duration);
                    localController.jumpHeight = origJ;
                }
                break;

            case PowerType.Invisibility:
                duration = 6f;
                {
                    var rends = player.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.enabled = false;
                    yield return new WaitForSeconds(duration);
                    foreach (var r in rends) r.enabled = true;
                }
                break;

            case PowerType.Dwarf:
                duration = 6f;
                {
                    Vector3 origScale = player.transform.localScale;
                    player.transform.localScale = origScale * 0.3f;
                    yield return new WaitForSeconds(duration);
                    player.transform.localScale = origScale;
                }
                break;

            case PowerType.Levitate:
                duration = 6f;
                {
                    float originalGravityMultiplier = localController.gravityMultiplier;
                    localController.gravityMultiplier = 0.1f;
                    yield return new WaitForSeconds(duration);
                    localController.gravityMultiplier = originalGravityMultiplier;
                }
                break;
        }

        isPowerEffectActive = false;
        Debug.Log($"Efecto de {type} terminado. El siguiente poder puede ser usado.");
    }

    private void ShiftPowersLeft()
    {
        if (powerSlots[1])
        {
            powerTypeSlots[0] = powerTypeSlots[1];
            powerTypeSlots[1] = null;

            if (powersHUD != null && powerTypeSlots[0].HasValue)
            {
                Sprite icon = GetPowerIcon(powerTypeSlots[0].Value);
                powersHUD.SetSlotSprite(0, icon);
                powersHUD.SetSlotSprite(1, null);
            }

            powerSlots[0] = true;
            powerSlots[1] = false;
        }
        else
        {
            powerTypeSlots[0] = null;
            if (powersHUD != null) powersHUD.SetSlotSprite(0, null);
            powerSlots[0] = false;
        }
    }

    public bool HasAvailableSlot()
    {
        for (int i = 0; i < powerSlots.Length; i++)
        {
            if (!powerSlots[i]) return true;
        }
        return false;
    }
}

// Enum para los tipos de poder
public enum PowerType
{
    Boost,
    JumpHigh,
    Invisibility,
    Dwarf,
    Levitate
}
