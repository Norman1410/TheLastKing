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


    // ======== ESPECTADOR ========
    [Header("Spectator")]
    [Tooltip("Nombre del Layer que usarán los espectadores (configurar colisiones en Project Settings > Physics).")]
    [SerializeField] private string spectatorLayerName = "Spectator";

    [Tooltip("Componentes de gameplay a desactivar al entrar en modo espectador (movimiento, dash, ataque, etc.).")]
    [SerializeField] private MonoBehaviour[] gameplayComponentsToDisable;

    [Tooltip("Altura (metros) para elevar al jugador cuando pasa a espectador, antes de apoyar con raycast.")]
    [SerializeField] private float spectatorRaiseMeters = 20f;

    [Tooltip("Desde qué altura máxima intentamos encontrar suelo (raycast downward) para apoyar al espectador.")]
    [SerializeField] private float spectatorRaycastDown = 200f;

    private NetworkVariable<bool> isSpectator = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsSpectator => isSpectator.Value;
    // ============================

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
            var actions = playerInput.actions;
            if (actions != null)
            {
                robAction = actions.FindAction("Rob", throwIfNotFound: false);
            }
            if (robAction != null)
            {
                robAction.performed += OnRobPerformed;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] Acción 'Rob' no encontrada en PlayerInput (no es error fatal).");
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
        if (IsSpectator) return; // en espectador no se roba
        
        if (targetPlayer != null && targetPlayer.HasCrown())
        {
            RobCrown();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        hasCrown.OnValueChanged += OnCrownChanged;
        isSpectator.OnValueChanged += OnSpectatorChanged;
        UpdateCrownVisual(hasCrown.Value);

        // Si el cliente entra y ya está en espectador (re-conexión o ronda en curso)
        if (IsSpectator && IsOwner)
        {
            // Aseguramos estado local de espectador
            TryEnableSpectatorLocal(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (hasCrown != null)
            hasCrown.OnValueChanged -= OnCrownChanged;
        isSpectator.OnValueChanged -= OnSpectatorChanged;
    }

    void OnCrownChanged(bool oldValue, bool newValue)
    {
        // Si estamos en espectador, la corona NO se muestra aunque el NV cambie
        UpdateCrownVisual(IsSpectator ? false : newValue);
        OnCrownStatusChanged?.Invoke(newValue);
    }

    void OnSpectatorChanged(bool wasSpectator, bool nowSpectator)
    {
        // Visualmente, espectador nunca muestra corona
        UpdateCrownVisual(nowSpectator ? false : hasCrown.Value);

        if (IsOwner)
        {
            TryEnableSpectatorLocal(nowSpectator);
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // Si soy espectador, NO hago lógicas de robo ni targeteo
        if (IsSpectator)
        {
            if (crosshair != null) crosshair.color = normalColor;
            targetPlayer = null;
            return;
        }

        // FALLBACK: Si no hay Input System, usar click izquierdo
        if (Input.GetMouseButtonDown(0)) // 0 = Click Izquierdo
        {
            if (targetPlayer != null && targetPlayer.HasCrown())
            {
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
            return;
        }
        // En espectador no se apunta ni se roba
        if (IsSpectator) return;

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
                    
                    Debug.DrawRay(ray.origin, ray.direction * robDistance, Color.green);
                    return;
                }
            }
        }
        
        targetPlayer = null;
        if (crosshair != null)
            crosshair.color = normalColor;
        
        Debug.DrawRay(ray.origin, ray.direction * robDistance, Color.yellow);
    }

    void RobCrown()
    {
        if (IsSpectator) return; // espectador no roba

        if (targetPlayer == null)
        {
            return;
        }
        
        var targetNetObj = targetPlayer.GetComponent<NetworkObject>();
        if (targetNetObj != null && targetNetObj.IsSpawned)
        {
            RobCrownServerRpc(targetNetObj.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RobCrownServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[Server] ServerRpc recibido de cliente {rpcParams.Receive.SenderClientId}");
        // Este jugador (emisor) no puede ser espectador para robar
        if (isSpectator.Value) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            var tpr = targetNetObj.GetComponent<PlayerRob>();
            if (tpr != null && tpr.hasCrown.Value && !tpr.isSpectator.Value)
            {
                // Verificar distancia server-side
                float distance = Vector3.Distance(transform.position, tpr.transform.position);
                if (distance > robDistance + 1f)
                {
                    return;
                }

                targetPlayer.hasCrown.Value = false;
                this.hasCrown.Value = true;

                Debug.Log($"[Server] {gameObject.name} robó corona de {targetPlayer.gameObject.name}!");
            }
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
    }

    public bool HasCrown()
    {
        // Un espectador nunca “cuenta” como que tiene corona para el juego
        if (IsSpectator) return false;
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
}
