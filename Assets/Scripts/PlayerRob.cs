using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerRob : NetworkBehaviour 
{
    [Header("Corona Settings")]
    [SerializeField] private GameObject crownObject;
    private NetworkVariable<bool> hasCrown = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Evento público para que el HUD pueda suscribirse
    public System.Action<bool> OnCrownStatusChanged;

    [Header("Rob Settings")]
    [SerializeField] private float robDistance = 3f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI Crosshair")]
    [SerializeField] private Image crosshair;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color canRobColor = Color.red;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    
    [Header("Crown Drop")]
    [SerializeField] private GameObject crownPickupPrefab;

    
    private PlayerRob targetPlayer;
    private InputAction robAction;
    private PlayerInput playerInput;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            robAction = playerInput.actions["Rob"];
            if (robAction != null)
            {
                robAction.performed += OnRobPerformed;
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] Acción 'Rob' no encontrada en PlayerInput!");
            }
        }
    }

    public override void OnDestroy()
    {
        if (robAction != null)
        {
            robAction.performed -= OnRobPerformed;
        }
    }

    void OnRobPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        
        Debug.Log($"[{gameObject.name}] Input ROB recibido! Target: {targetPlayer != null}");
        
        if (targetPlayer != null && targetPlayer.HasCrown())
        {
            RobCrown();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        hasCrown.OnValueChanged += OnCrownChanged;
        UpdateCrownVisual(hasCrown.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (hasCrown != null)
            hasCrown.OnValueChanged -= OnCrownChanged;
    }

    void OnCrownChanged(bool oldValue, bool newValue)
    {
        UpdateCrownVisual(newValue);
        Debug.Log($"[{gameObject.name}] Corona: {oldValue} -> {newValue}");
        
        // Disparar evento para que el HUD se actualice
        OnCrownStatusChanged?.Invoke(newValue);
    }

    void Update()
    {
        if (!IsOwner) return;

        // FALLBACK: Si no hay Input System, usar click izquierdo
        if (Input.GetMouseButtonDown(0)) // 0 = Click Izquierdo
        {
            if (targetPlayer != null && targetPlayer.HasCrown())
            {
                Debug.Log($"[{gameObject.name}] Robo directo (fallback)");
                RobCrown();
            }
        }

        if (hasCrown.Value)
        {
            if (crosshair != null)
                crosshair.color = normalColor;
            targetPlayer = null;
            return;
        }
        
        DetectTargetPlayer();
    }
    
    void DetectTargetPlayer()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning($"[{gameObject.name}] playerCamera es null!");
            return;
        }
            
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, robDistance, playerLayer))
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            
            if (distance <= robDistance)
            {
                PlayerRob player = hit.collider.GetComponent<PlayerRob>();
                
                if (player != null && player != this && player.HasCrown())
                {
                    targetPlayer = player;
                    
                    if (crosshair != null)
                        crosshair.color = canRobColor;
                    
                    // Debug visual
                    Debug.DrawRay(ray.origin, ray.direction * robDistance, Color.green);
                    
                    return;
                }
            }
        }
        
        targetPlayer = null;
        if (crosshair != null)
            crosshair.color = normalColor;
        
        // Debug visual cuando no hay objetivo
        Debug.DrawRay(ray.origin, ray.direction * robDistance, Color.yellow);
    }

    void RobCrown()
    {
        if (targetPlayer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] targetPlayer es null!");
            return;
        }
        
        var targetNetObj = targetPlayer.GetComponent<NetworkObject>();
        if (targetNetObj != null && targetNetObj.IsSpawned)
        {
            Debug.Log($"[{gameObject.name}] Enviando ServerRpc para robar...");
            RobCrownServerRpc(targetNetObj.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RobCrownServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[Server] ServerRpc recibido de cliente {rpcParams.Receive.SenderClientId}");

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            var targetPlayer = targetNetObj.GetComponent<PlayerRob>();
            if (targetPlayer != null && targetPlayer.hasCrown.Value)
            {
                // Verificar distancia server-side
                float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
                if (distance > robDistance + 1f)
                {
                    Debug.LogWarning($"[Server] Robo rechazado: distancia {distance:F2} > {robDistance}");
                    return;
                }

                targetPlayer.hasCrown.Value = false;
                this.hasCrown.Value = true;

                Debug.Log($"[Server] {gameObject.name} robó corona de {targetPlayer.gameObject.name}!");
            }
            else
            {
                Debug.LogWarning($"[Server] Objetivo no tiene corona o es null");
            }
        }
        else
        {
            Debug.LogWarning($"[Server] NetworkObject {targetNetworkObjectId} no encontrado");
        }
    }
    
    // --- TRAP LOGIC: Client/Trap requests the Server to drop the crown ---
    [ServerRpc(RequireOwnership = false)]
    public void DropCrownServerRpc(Vector3 direction, float force, ServerRpcParams rpcParams = default)
    {
        if (!hasCrown.Value) return;

        hasCrown.Value = false;
        
        // 2. Tell all clients to visually drop the crown and apply physics
        DropCrownClientRpc(direction, force);
        
        Debug.Log($"[Server] {gameObject.name} hit a trap and dropped the crown!");
    }


    // --- TRAP LOGIC: Server tells all Clients to handle the crown's physics and animation ---
    [ClientRpc]
    private void DropCrownClientRpc(Vector3 direction, float force)
    {
        if (crownObject == null) return;
        
        Rigidbody crownRb = crownObject.GetComponent<Rigidbody>();
        Collider crownCol = crownObject.GetComponent<Collider>();
        
        if (crownRb != null && crownCol != null)
        {
            crownObject.transform.SetParent(null);
            crownRb.isKinematic = false;
            crownCol.isTrigger = false;
            
            // Apply force
            crownRb.AddForce(direction * force, ForceMode.Impulse);
            
            // Apply random spin
            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ).normalized * force * 0.5f; 
            
            crownRb.AddTorque(randomTorque, ForceMode.Impulse);

            Destroy(crownObject.gameObject, 15f); 
        }
    }


    void UpdateCrownVisual(bool active)
    {
        if (crownObject == null) return;

        if (active)
        {
            // Crown is being worn: Attach, position, and disable physics
            
            crownObject.transform.SetParent(this.transform); 
            crownObject.transform.localPosition = new Vector3(0, 1.5f, 0); // Adjust this position
            crownObject.transform.localRotation = Quaternion.identity;
            
            // Get components for physics setup
            Rigidbody crownRb = crownObject.GetComponent<Rigidbody>();
            Collider crownCol = crownObject.GetComponent<Collider>();
            
            // Disable physics while worn
            if (crownRb != null) crownRb.isKinematic = true;
            if (crownCol != null) crownCol.isTrigger = true;
            
            crownObject.SetActive(true);
        }
        else
        {
            // Crown is NOT worn: If it's still attached, we hide it.
            if (crownObject.transform.parent == this.transform)
            {
                crownObject.SetActive(false);
            }
        }
    }

    public void SetCrownDirect(bool value)
    {
        if (!IsServer)
        {
            Debug.LogWarning("SetCrownDirect debe llamarse solo desde el servidor!");
            return;
        }
        
        hasCrown.Value = value;
        Debug.Log($"[Server] SetCrownDirect: {gameObject.name} corona = {value}");
    }

    public bool HasCrown()
    {
        return hasCrown.Value;
    }

    public void SetCrown(bool value)
    {
        if (IsServer)
        {
            hasCrown.Value = value;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = hasCrown.Value ? Color.yellow : Color.cyan;
            Vector3 direction = playerCamera.transform.forward;
            Gizmos.DrawRay(playerCamera.transform.position, direction * robDistance);
            Gizmos.DrawWireSphere(playerCamera.transform.position + direction * robDistance, 0.3f);
        }
    }

    //void OnGUI()
    //{
    //    if (!IsOwner) return;
//
    //    GUILayout.BeginArea(new Rect(10, 10, 300, 100));
    //    GUILayout.Label($"HasCrown: {hasCrown.Value}");
    //    GUILayout.Label($"Target: {(targetPlayer != null ? "SÍ" : "NO")}");
    //    GUILayout.Label($"Camera: {(playerCamera != null ? "OK" : "NULL")}");
    //    if (targetPlayer != null)
    //    {
    //        float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
    //        GUILayout.Label($"Distancia: {dist:F2}m / {robDistance}m");
    //    }
    //    GUILayout.EndArea();
    //}
}