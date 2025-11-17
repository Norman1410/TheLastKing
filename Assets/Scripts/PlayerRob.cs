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

    // === Spectator additions ===
    [Header("Spectator")]
    [Tooltip("Physics Layer to assign when entering spectator mode")]
    [SerializeField] private string spectatorLayerName = "Spectator";

    private NetworkVariable<bool> isSpectator = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Tooltip("Gameplay components to disable when in spectator (e.g., PlayerMovement, AttackController, Dash)")]
    [SerializeField] private MonoBehaviour[] gameplayComponentsToDisable;

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
        if (isSpectator.Value) return; // guard: espectador no roba
        
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
        // Un espectador no muestra corona
        if (isSpectator.Value && crownObject) crownObject.SetActive(false);
        OnCrownStatusChanged?.Invoke(newValue);
    }

    void Update()
    {
        if (!IsOwner) return;

        // Si es espectador: bloquear interacción y dejar crosshair neutro
        if (isSpectator.Value)
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
            RobCrownServerRpc(targetNetObj.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RobCrownServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
    {
        // Guard: atacante espectador no puede robar
        if (isSpectator.Value)
        {
            Debug.LogWarning("[Server] Robo rechazado: atacante es espectador.");
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            var targetPlayer = targetNetObj.GetComponent<PlayerRob>();
            // Guard: objetivo espectador no es válido
            if (targetPlayer != null && targetPlayer.IsSpectator())
            {
                Debug.LogWarning("[Server] Robo rechazado: objetivo es espectador.");
                return;
            }

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
                Debug.LogWarning("[Server] Objetivo no tiene corona o es null");
            }
        }
        else
        {
            Debug.LogWarning($"[Server] NetworkObject {targetNetworkObjectId} no encontrado");
        }
    }

    void UpdateCrownVisual(bool active)
    {
        if (crownObject != null)
        {
            crownObject.SetActive(active && !isSpectator.Value);
        }
        else if (active)
        {
            Debug.LogWarning($"[{gameObject.name}] crownObject no asignado pero debería mostrar corona!");
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

    public bool HasCrown() => hasCrown.Value;
    public bool IsSpectator() => isSpectator.Value;

    public void SetCrown(bool value)
    {
        if (IsServer)
        {
            hasCrown.Value = value;
        }
    }

    // === Spectator API ===
    [ServerRpc(RequireOwnership = false)]
    public void EnterSpectatorServerRpc(ServerRpcParams rpc = default) => EnterSpectatorServer();

    public void EnterSpectatorServer()
    {
        if (!IsServer) return;

        // 1) marcar NV y quitar corona
        isSpectator.Value = true;
        SetCrownDirect(false);

        // 2) cambiar capa para ignorar trampas/pickups/combate
        if (!string.IsNullOrEmpty(spectatorLayerName))
            gameObject.layer = LayerMask.NameToLayer(spectatorLayerName);

        // 3) desactivar gameplay server-authoritative si aplica
        TryDisableGameplayServer();

        // 4) mover a punto seguro de espectador
        TeleportToSpectatorPoint();

        // 5) avisar al dueño para activar cámara de espectador/ocultar HUD
        EnterSpectatorClientRpc();
    }

    [ClientRpc]
    void EnterSpectatorClientRpc()
    {
        try
        {
            if (gameplayComponentsToDisable != null)
                foreach (var mb in gameplayComponentsToDisable)
                    if (mb) mb.enabled = false;

            // Ocultar HUDs comunes (por si no lo hizo el LobbyState)
            var crownHud = UnityEngine.Object.FindAnyObjectByType<CrownHud>();
            if (crownHud) crownHud.gameObject.SetActive(false);
            var powersHud = UnityEngine.Object.FindAnyObjectByType<PowersHUD>();
            if (powersHud) powersHud.gameObject.SetActive(false);

            var spec = GetComponent<SpectatorController>();
            if (spec) spec.EnableSpectatorLocal(true);
        }
        catch { }
    }

    void TryDisableGameplayServer()
    {
        // Si tu combate/daño es autoritativo en server, desactívalo aquí.
        // Este ejemplo no requiere nada extra.
    }

    void TeleportToSpectatorPoint()
    {
        // Simple: súbelo sobre el mapa y ajusta a suelo
        Vector3 pos = transform.position + new Vector3(0, 20f, 0);
        if (Physics.Raycast(pos, Vector3.down, out var hit, 200f))
            pos = hit.point + Vector3.up * 10f;

        var cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        if (cc) cc.enabled = true;
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

    void OnGUI()
    {
        if (!IsOwner) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 120));
        GUILayout.Label($"HasCrown: {hasCrown.Value}");
        GUILayout.Label($"IsSpectator: {isSpectator.Value}");
        GUILayout.Label($"Target: {(targetPlayer != null ? "SÍ" : "NO")} ");
        GUILayout.Label($"Camera: {(playerCamera != null ? "OK" : "NULL")} ");
        if (targetPlayer != null)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            GUILayout.Label($"Distancia: {dist:F2}m / {robDistance}m");
        }
        GUILayout.EndArea();
    }
}
