using UnityEngine;
using System;

public class PowerUp : MonoBehaviour
{
    public Action onPicked;    // Evento que el manager usar�
    public float duration = 5f;
    // Guard to prevent the same powerup being picked multiple times (multiple colliders / frames)
    private bool isPicked = false;

    public virtual void Activate(GameObject player)
    {
        // Aqu� se define qu� hace el power-up; cada hijo lo sobrescribir�
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Prevent double pickup (e.g., player with multiple colliders or duplicate trigger calls)
        if (isPicked) return;

        // If PowerManager exists and has no available slot, don't pick up
        if (PowerManager.Instance != null && !PowerManager.Instance.HasAvailableSlot())
        {
            // Optionally log for debug
            // Debug.Log("Power pickup attempted but no available slots.");
            return;
        }

        isPicked = true;

        // Disable collider immediately to avoid further trigger events
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

    Debug.Log($"PowerUp: {gameObject.name} picked by {other.gameObject.name}");

    // Notify PowerManager so HUD can update visually (pass this instance so mapping can infer icon)
    PowerManager.Instance?.AddPower(null, gameObject);

    Activate(other.gameObject);
    onPicked?.Invoke();

        // Deactivate the picked object (original behavior)
        gameObject.SetActive(false);
    }
}
