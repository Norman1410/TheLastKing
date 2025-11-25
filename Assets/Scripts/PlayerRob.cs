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

    // ======== ESPECTADOR (NUEVO) ========
    [Header("Spectator")]
    [Tooltip("Physics Layer para modo espectador (configura colisiones en Project Settings > Physics)")]
    [SerializeField] private string spectatorLayerName = "Spectator";

    private NetworkVariable<bool> isSpectator = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Tooltip("Componentes de gameplay a desactivar en espectador (ej: PlayerMovement, Dash, AttackController)")]
    [SerializeField] private MonoBehaviour[] gameplayComponentsToDisable;
    // ====================================

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
        if (isSpectator.Value) return; // espectador no puede robar
        
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

        // Si es espectador: no interactúa ni detecta objetivos
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

        // Atacante espectador no puede robar
        if (isSpectator.Value)
        {
            Debug.LogWarning("[Server] Robo rechazado: atacante es espectador.");
            return;
        }
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            var targetPlayer = targetNetObj.GetComponent<PlayerRob>();
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
                Debug.LogWarning($"[Server] Objetivo no tiene corona o es null");
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
            // Si es espectador, no muestra corona aunque la NV estuviera activa
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

    // ======== ESPECTADOR: API PÚBLICA ========
    public bool IsSpectator() => isSpectator.Value;

    [ServerRpc(RequireOwnership = false)]
    public void EnterSpectatorServerRpc(ServerRpcParams rpc = default) => EnterSpectatorServer();

    public void EnterSpectatorServer()
{
    if (!IsServer) return;

    isSpectator.Value = true;
    SetCrownDirect(false);

    // Cambiar a capa de espectador para aislar colisiones si quieres
    if (!string.IsNullOrEmpty(spectatorLayerName))
        gameObject.layer = LayerMask.NameToLayer(spectatorLayerName);

    // DESACTIVAR física de caída
    var cc = GetComponent<CharacterController>();
    if (cc) cc.enabled = false;

    var rb = GetComponent<Rigidbody>();
    if (rb)
    {
        rb.isKinematic = true;     // evita fuerzas
        rb.useGravity  = false;    // sin gravedad
        rb.linearVelocity    = Vector3.zero; // limpia
        rb.angularVelocity = Vector3.zero;
    }

    TryDisableGameplayServer(); // tu desactivación de scripts de movimiento/ataque

    // Teleport arriba y “snap” final
    TeleportToSpectatorPoint();

    // Opcional: bloquear colisiones con el suelo (si usas collider)
    var col = GetComponent<Collider>();
    if (col) col.enabled = false;  // si quieres volar/atravesar
                                   // (o deja enabled y confía en isKinematic + sin gravedad)

    // Aviso al cliente: activa cámara e INHABILITA también localmente física/juego
    EnterSpectatorClientRpc();
}


    [ClientRpc]
void EnterSpectatorClientRpc()
{
    // Apaga scripts de gameplay
    if (gameplayComponentsToDisable != null)
        foreach (var mb in gameplayComponentsToDisable)
            if (mb) mb.enabled = false;

    // Refuerza física local
    var cc = GetComponent<CharacterController>();
    if (cc) cc.enabled = false;

    var rb = GetComponent<Rigidbody>();
    if (rb)
    {
        rb.isKinematic = true;
        rb.useGravity  = false;
        rb.linearVelocity    = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    var col = GetComponent<Collider>();
    if (col) col.enabled = false;

    // HUD y cámara
    var crownHud = UnityEngine.Object.FindAnyObjectByType<CrownHud>(); if (crownHud) crownHud.gameObject.SetActive(false);
    var powersHud = UnityEngine.Object.FindAnyObjectByType<PowersHUD>(); if (powersHud) powersHud.gameObject.SetActive(false);

    var spec = GetComponent<SpectatorController>();
    if (spec) spec.EnableSpectatorLocal(true);
}

    void TryDisableGameplayServer()
    {
        // Si hay lógica autoritativa de combate/daño en server, desactívala aquí.
        // Si todo está en clientes con validación, no hace falta nada.
    }

    void TeleportToSpectatorPoint()
    {
        // Sube 20m y ajusta a suelo con offset
        Vector3 pos = transform.position + new Vector3(0, 20f, 0);
        if (Physics.Raycast(pos, Vector3.down, out var hit, 200f))
            pos = hit.point + Vector3.up * 10f;

        var cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        if (cc) cc.enabled = true;
    }

    //void OnGUI()
    //{
    //    if (!IsOwner) return;
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
