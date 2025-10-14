using UnityEngine;
using System.Collections;

public class RotationController : MonoBehaviour
{
    // Variables visible in the Inspector
    public float rotationSpeed = 120f; // Degrees per second
    public float rotationDuration = 3f;  // How long it rotates
    
    private bool isRotating = false;

    // Called when the player steps into the trigger zone
    private void OnTriggerEnter(Collider other)
    {
        // Check for the "Player" tag and if it's not already rotating
        if (other.CompareTag("Player") && !isRotating)
        {
            StartCoroutine(RotateTrap());
        }
    }

    IEnumerator RotateTrap()
    {
        isRotating = true;
        float timer = 0f;

        // Loop while the timer is less than the desired duration
        while (timer < rotationDuration)
        {
            // Calculate how much time passed since the last frame
            float rotationAmount = rotationSpeed * Time.deltaTime;
            
            // Apply rotation around the Y-axis (vertical)
            transform.Rotate(0, rotationAmount, 0);

            // Increase the timer
            timer += Time.deltaTime;

            // Wait until the next frame to continue the loop
            yield return null; 
        }

        isRotating = false; // Reset the trap
    }
}