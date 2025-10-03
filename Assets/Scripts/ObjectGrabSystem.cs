using UnityEngine;
using UnityEngine.UI;

public class ObjectGrabSystem : MonoBehaviour
{
    [Header("Grab Settings")]
    public float grabDistance = 5f; // Increased distance
    public KeyCode grabKey = KeyCode.Mouse0;
    public Vector3 holdOffset = new Vector3(0, 1f, 2f);
    
    [Header("UI References")]
    public Image objectHeldImage;
    public Image objectNotHeldImage;
    
    [Header("Debug")]
    public bool showDebugRay = true;
    public Color debugRayColor = Color.green;
    
    private GameObject currentObject;
    private bool isHoldingObject = false;
    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;

    }

    void Update()
    {
        HandleGrabInput();
        UpdateUI();
        
        if (showDebugRay)
        {
            Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * grabDistance, debugRayColor);
        }
    }

    void HandleGrabInput()
    {
        if (Input.GetKeyDown(grabKey))
        {
            
            if (isHoldingObject)
            {
                ReleaseObject();
            }
            else
            {
                AttemptGrab();
            }
        }
    }

    void AttemptGrab()
    {
        RaycastHit hit;
        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;
        
        bool hitSomething = Physics.Raycast(rayOrigin, rayDirection, out hit, grabDistance);
        
        
        if (hitSomething)
        {
            
            if (hit.collider.CompareTag("Grabbable"))
            {
                GrabObject(hit.collider.gameObject);
            }
          
        }
    }

    void GrabObject(GameObject objectToGrab)
    {
        currentObject = objectToGrab;
        isHoldingObject = true;
        
        
        // Disable physics temporarily
        Rigidbody rb = currentObject.GetComponent<Rigidbody>();
        if (rb != null) 
        {
            rb.isKinematic = true;
        }
        
        // Make object child of player
        currentObject.transform.SetParent(transform);
        currentObject.transform.localPosition = holdOffset;
        currentObject.transform.localRotation = Quaternion.identity;
        
        // Disable collider to prevent interference
        Collider col = currentObject.GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = false;

        }

    }

    void ReleaseObject()
    {
        if (currentObject != null)
        {

            // Re-enable collider
            Collider col = currentObject.GetComponent<Collider>();
            if (col != null) 
            {
                col.enabled = true;

            }
            
            // Re-enable physics
            Rigidbody rb = currentObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = transform.forward * 3f;

            }
            
            // Remove parent relationship
            currentObject.transform.SetParent(null);

            currentObject = null;
            isHoldingObject = false;

        }
    }

    void UpdateUI()
    {
        if (objectHeldImage != null)
        {
            bool wasActive = objectHeldImage.gameObject.activeSelf;
            objectHeldImage.gameObject.SetActive(isHoldingObject);

        }

        if (objectNotHeldImage != null)
        {
            bool wasActive = objectNotHeldImage.gameObject.activeSelf;
            objectNotHeldImage.gameObject.SetActive(!isHoldingObject);
        }
    }

    // Visual debug in scene view
    void OnDrawGizmosSelected()
    {
        if (showDebugRay && playerCamera != null)
        {
            Gizmos.color = debugRayColor;
            Gizmos.DrawLine(playerCamera.transform.position, 
                           playerCamera.transform.position + playerCamera.transform.forward * grabDistance);
            Gizmos.DrawWireSphere(playerCamera.transform.position + playerCamera.transform.forward * grabDistance, 0.2f);
        }
    }
}