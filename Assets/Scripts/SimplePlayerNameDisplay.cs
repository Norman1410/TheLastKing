using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

// Displays a player's nickname above their head. Behaves differently per client:
// - The local owner will not see their own nameplate (keeps view clean)
// - Other clients will see the name and it will face the local camera (billboard)
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

    // Networking references
    private PlayerRobNetworked playerNetworked;
    private NetworkObject netObj;

    private void Start()
    {
        mainCamera = Camera.main;

        // If no player transform assigned, try to find parent or tagged player
        if (playerTransform == null)
        {
            playerTransform = transform.parent != null ? transform.parent : GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (playerTransform != null)
        {
            initialOffset = transform.position - playerTransform.position;
        }

        // Try to get the networked player component on parent (if this is on a player prefab)
        playerNetworked = GetComponentInParent<PlayerRobNetworked>();
        if (playerNetworked != null)
        {
            netObj = playerNetworked.GetComponent<NetworkObject>();
            // Subscribe to networked nickname changes so UI updates only when needed
            playerNetworked.SubscribeToNickname(OnNetworkNicknameChanged);
            // Initialize display with current value
            SetNicknameText(playerNetworked.GetNetworkedNickname());
        }
        else
        {
            // Fallback: use local nickname manager or preview
            SetNicknameText(usePreviewName ? previewName : NicknameManager.GetCurrentNickname());
        }
    }

    private void Update()
    {
        // Follow player while maintaining your manual position offset
        if (playerTransform != null)
        {
            transform.position = playerTransform.position + initialOffset;
        }

        // Determine visibility: hide the nameplate for the local owner
        bool shouldShow = true;
        if (netObj != null && NetworkManager.Singleton != null)
        {
            // If this object's owner is the local client, don't show the nameplate
            shouldShow = netObj.OwnerClientId != NetworkManager.Singleton.LocalClientId;
        }

        if (worldCanvas != null && worldCanvas.gameObject.activeSelf != shouldShow)
        {
            worldCanvas.gameObject.SetActive(shouldShow);
        }

        // Always face camera for readability from any angle (billboard per viewer)
        if (lookAtCamera && mainCamera != null && worldCanvas != null && worldCanvas.gameObject.activeSelf)
        {
            Vector3 directionToCamera = mainCamera.transform.position - worldCanvas.transform.position;
            worldCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }

    private void OnNetworkNicknameChanged(string previous, string current)
    {
        SetNicknameText(current);
    }

    private void SetNicknameText(string text)
    {
        if (nicknameText == null) return;
        string display = string.IsNullOrEmpty(text) ? (usePreviewName ? previewName : "Player") : text;
        if (nicknameText.text != display)
        {
            nicknameText.text = display;
        }
    }

    private void OnDestroy()
    {
        if (playerNetworked != null)
        {
            playerNetworked.UnsubscribeFromNickname(OnNetworkNicknameChanged);
        }
    }
}