using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimplePlayerNameDisplay : MonoBehaviour
{
    [Header("UI References - Drag manually created UI here")]
    public Canvas worldCanvas; // World Space Canvas (create manually)
    public Image backgroundPanel; // Background image (create manually)
    public TextMeshProUGUI nicknameText; // Text component (create manually)
    
    [Header("Follow Settings")]
    public Transform playerTransform; // The player to follow
    public bool lookAtCamera = true; // Make nameplate face camera
    
    [Header("Preview")]
    public bool usePreviewName = false;
    public string previewName = "TestPlayer";
    
    private Camera mainCamera;
    private Vector3 initialOffset; // Store initial position relative to player
    
    private void Start()
    {
        mainCamera = Camera.main;
        
        // If no player transform assigned, try to find it
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        // Store initial offset based on your manual positioning
        if (playerTransform != null)
        {
            initialOffset = transform.position - playerTransform.position;
        }
        
        UpdateNickname();
    }
    
    private void Update()
    {
        // Follow player while maintaining your manual position offset
        if (playerTransform != null)
        {
            transform.position = playerTransform.position + initialOffset;
        }
        
        // Always face camera for readability from any angle
        if (lookAtCamera && mainCamera != null && worldCanvas != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - worldCanvas.transform.position;
            worldCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
        
        // Update nickname
        UpdateNickname();
    }
    
    private void UpdateNickname()
    {
        if (nicknameText != null)
        {
            string displayName = usePreviewName ? previewName : NicknameManager.GetCurrentNickname();
            if (nicknameText.text != displayName)
            {
                nicknameText.text = displayName;
            }
        }
    }
}