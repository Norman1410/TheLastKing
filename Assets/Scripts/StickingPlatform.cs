using UnityEngine;
using System.Collections;
using System.Diagnostics;
using System; // Needed for Coroutines

public class StickingPlatform : MonoBehaviour
{
    public float disableDuration = 3f; // How long the player is stuck
   [SerializeField] private float slowSpeed = 0.5f; // Speed to reduce to when on the platform
    [SerializeField] private float restoreDelay = 3f; // Time after which speed is restored

    // This function is called when another object enters the trigger collider
    void OnTriggerEnter(Collider other)
    {
        UnityEngine.Debug.Log("Entered.");
        // Check if the colliding object is the player
        if (other.CompareTag("Player"))
        {
            UnityEngine.Debug.Log("Player entered the sticking platform.");
            // Try to get the PlayerMovement script from the player
            FirstPersonController player = other.GetComponent<FirstPersonController>();

            if (player != null)
            {
                UnityEngine.Debug.Log("Found Script.");
                // Guardar la velocidad original
                float originalSpeed = player.GetWalkSpeed();

                // Reducir la velocidad temporalmente
                player.SetWalkSpeed(slowSpeed);

                // Restaurarla después de unos segundos
                StartCoroutine(RestoreSpeed(player, originalSpeed));

            }
            else
            {
                UnityEngine.Debug.LogWarning("PlayerMovement script not found on the player.");
            }
        }
    }

    private IEnumerator RestoreSpeed(FirstPersonController player, float originalSpeed)
    {
        yield return new WaitForSeconds(restoreDelay);
        player.SetWalkSpeed(originalSpeed);
    }
}