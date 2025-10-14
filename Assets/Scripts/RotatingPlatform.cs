using UnityEngine;

public class RotatingPlatform : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f; // degrees per second
    private bool shouldRotate = false;

    // References the Rotating_Platform object's transform
    private Transform platformTransform; 

    private void Start()
    {
        // Get the parent's transform (the visual/main platform)
        platformTransform = transform.parent;
        
        if (platformTransform == null)
        {
            Debug.LogError("RotatingPlatform script is not parented correctly and cannot find the platform to rotate!");
            enabled = false;
            return;
        }

        // Optional: Check if the parent platform has the correct tag
        if (!platformTransform.CompareTag("MovingPlatform"))
        {
            Debug.LogWarning($"{platformTransform.name} does not have the 'MovingPlatform' tag! Rotation may fail.");
        }
    }

    private void Update()
    {
        // Only rotate if flagged
        if (shouldRotate && platformTransform != null)
        {
            // CRITICAL FIX: Rotate the parent platform's transform
            platformTransform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only trigger if a player steps on it AND the parent has the required tag
        if (other.CompareTag("Player") && platformTransform.CompareTag("MovingPlatform"))
        {
            Debug.Log("Player stepped on platform — STARTING SPIN & PARENTING!");
            shouldRotate = true;

            // Make player move with the platform by setting their parent to the ROTATING_PLATFORM
            other.transform.SetParent(platformTransform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player left platform — STOPPING SPIN & UNPARENTING!");
            shouldRotate = false;

            // Release player from platform
            other.transform.SetParent(null);
        }
    }
}