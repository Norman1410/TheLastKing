using System;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

// Networked version of PlayerRob. Attach to player prefabs that have a NetworkObject.
public class PlayerRobNetworked : NetworkBehaviour
{
    // Networked nickname - other clients will see this value
    public NetworkVariable<string> PlayerNickname = new NetworkVariable<string>("Player");

    [Header("Crown Visual")]
    [SerializeField] private GameObject crownObject; // child object to enable/disable

    [Header("Rob Settings")]
    [SerializeField] private float robDistance = 3f;
    [SerializeField] private KeyCode robKey = KeyCode.Mouse0;
    [SerializeField] private LayerMask playerLayer;

    [Header("UI Crosshair")]
    [SerializeField] private Image crosshair;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color canRobColor = Color.red;

    [Header("Camera (optional)")]
    [SerializeField] private Camera playerCamera;

    // Reference to the centralized CrownManager's NetworkVariable
    private NetworkVariable<ulong> CrownOwner => CrownManager.Instance != null ? CrownManager.Instance.CrownOwner : null;

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        UpdateCrownVisual(LocalHasCrown());

        // Subscribe to crown changes
        if (CrownManager.Instance != null)
        {
            CrownManager.Instance.CrownOwner.OnValueChanged += OnCrownOwnerChanged;
        }

        // If this is the server or owner, we can set an initial nickname
        // Typically the server would set nicknames on spawn. For convenience, owner will set its saved nickname when it becomes the local client.
        if (IsOwner)
        {
            // Tell server to set this client's nickname from local PlayerPrefs (via NicknameManager)
            SetNicknameServerRpc(NicknameManager.GetCurrentNickname());
        }
    }

    public override void OnDestroy()
    {
        if (CrownManager.Instance != null)
        {
            CrownManager.Instance.CrownOwner.OnValueChanged -= OnCrownOwnerChanged;
        }
        base.OnDestroy();
    }

    private void OnCrownOwnerChanged(ulong previous, ulong current)
    {
        // When crown owner changes, update visual for this player
        UpdateCrownVisual(LocalHasCrown());
    }

    private void Update()
    {
        if (!IsOwner) return; // only the local player should perform raycasts and input

        if (playerCamera == null) return;

        // detect target
        var target = DetectTargetWithCrown();
        if (target != null)
        {
            if (crosshair != null) crosshair.color = canRobColor;
            if (Input.GetKeyDown(robKey))
            {
                // Request server to perform rob
                RequestRobServerRpc(target.OwnerClientId);
            }
        }
        else
        {
            if (crosshair != null) crosshair.color = normalColor;
        }
    }

    // Returns the networked PlayerRobNetworked on the hit if it currently has crown
    private PlayerRobNetworked DetectTargetWithCrown()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, robDistance, playerLayer))
        {
            var other = hit.collider.GetComponentInParent<PlayerRobNetworked>();
            if (other != null && other != this)
            {
                if (CrownManager.Instance != null && CrownManager.Instance.CrownOwner.Value == other.OwnerClientId) return other;
            }
        }
        return null;
    }

    // Called on client -> request server to attempt robber action on targetClientId
    [ServerRpc(RequireOwnership = false)]
    private void RequestRobServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        var requesterId = rpcParams.Receive.SenderClientId;

    // Validate target exists and has crown
    if (CrownManager.Instance == null) return;
    if (CrownManager.Instance.CrownOwner.Value != targetClientId) return; // target doesn't have crown

        // Get NetworkObject for requester and target
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(requesterId, out var requesterClient)) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var targetClient)) return;

        var requesterObj = requesterClient.PlayerObject;
        var targetObj = targetClient.PlayerObject;
        if (requesterObj == null || targetObj == null) return;

        // Validate distance on server side
        float distance = Vector3.Distance(requesterObj.transform.position, targetObj.transform.position);
        if (distance > robDistance + 0.5f) return; // too far (little tolerance)

    // Transfer crown ownership
    CrownManager.Instance.CrownOwner.Value = requesterId;

        // Optionally: you can send a ClientRpc to show a message, for now clients will see visuals via CrownOwner change
    }

    // helper: does this local player have crown?
    private bool LocalHasCrown()
    {
        if (NetworkManager.Singleton == null) return false;
    if (CrownManager.Instance == null) return false;
    return CrownManager.Instance.CrownOwner.Value == OwnerClientId;
    }

    // ServerRpc used by owner to set their nickname on the server so it replicates to others
    [ServerRpc(RequireOwnership = true)]
    public void SetNicknameServerRpc(string nickname, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(nickname)) nickname = "Player";
        PlayerNickname.Value = nickname;
    }

    // Helper for other components to read the nickname safely
    public string GetNetworkedNickname()
    {
        return PlayerNickname != null ? PlayerNickname.Value : "Player";
    }

    // Allow other local components to subscribe to nickname changes without accessing the field directly
    public void SubscribeToNickname(NetworkVariable<string>.OnValueChangedDelegate callback)
    {
        if (PlayerNickname != null) PlayerNickname.OnValueChanged += callback;
    }

    public void UnsubscribeFromNickname(NetworkVariable<string>.OnValueChangedDelegate callback)
    {
        if (PlayerNickname != null) PlayerNickname.OnValueChanged -= callback;
    }

    private void UpdateCrownVisual(bool has)
    {
        if (crownObject != null) crownObject.SetActive(has);
    }
}
