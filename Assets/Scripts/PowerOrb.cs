using UnityEngine;

public class PowerOrb : MonoBehaviour
{
    [Header("Power Settings")]
    public PowerType powerType; 
    public float floatSpeed = 5f; // Speed of up/down movement
    public float floatHeight = 0.05f; // How high/low it moves
    public float respawnTime = 3f; // Time in seconds to respawn 
    
    private PowerManager powerManager;
    private bool hasBeenCollected = false;
    private MeshRenderer meshRenderer;
    private Collider orbCollider;
    private Vector3 startPosition; // Starting position for floating effect
    
    private void Start()
    {
        powerManager = FindAnyObjectByType<PowerManager>();
        
        if (powerManager == null)
        {
            Debug.LogError("PowerManager not found in scene!");
        }
        
        // Get components to show/hide the orb
        meshRenderer = GetComponent<MeshRenderer>();
        orbCollider = GetComponent<Collider>();
        
        // Store starting position for floating effect
        startPosition = transform.position;
    }
    
    private void Update()
    {
        // Create floating effect - move up and down
        if (!hasBeenCollected)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasBeenCollected)
        {
            // mark immediately to avoid multiple triggers racing
            // but only if there is an available slot; otherwise ignore
            if (powerManager != null && !powerManager.HasAvailableSlot())
            {
                Debug.Log("PowerOrb: player touched but no available power slots.");
                return;
            }

            hasBeenCollected = true;

            // disable collider immediately to avoid duplicate triggers
            if (orbCollider != null) orbCollider.enabled = false;

            // hide visuals immediately
            if (meshRenderer != null) meshRenderer.enabled = false;

            Debug.Log($"PowerOrb: {gameObject.name} collected by {other.gameObject.name}");

            // perform power collection
            CollectPower();
        }
    }
    
    private void CollectPower()
    {
        if (powerManager != null && powerManager.HasAvailableSlot())
        {
            // Add power to HUD (pass this instance so PowerManager can dedupe)
            powerManager.AddPower(powerType, gameObject);

            // Schedule respawn
            StartCoroutine(RespawnOrb());

            Debug.Log($"Power {powerType} collected. Will respawn in {respawnTime} seconds.");
        }
        else
        {
            Debug.Log("Can't collect more powers, slots are full!");
            // revert state so orb can be collected later
            ShowOrb();
        }
    }
    
    private void HideOrb()
    {
        // Hide orb visually
        if (meshRenderer != null) meshRenderer.enabled = false;
        
        // Disable collider so it can't be touched
        if (orbCollider != null) orbCollider.enabled = false;
    }
    
    private void ShowOrb()
    {
        // Show orb again
        if (meshRenderer != null) meshRenderer.enabled = true;
        
        // Re-enable collider
        if (orbCollider != null) orbCollider.enabled = true;
        
        // Allow collection again
        hasBeenCollected = false;
    }
    
    private System.Collections.IEnumerator RespawnOrb()
    {
        // Wait for respawn time
        yield return new WaitForSeconds(respawnTime);
        
        // Show orb again
        ShowOrb();
        
        Debug.Log($"Power {powerType} has respawned!");
    }
}