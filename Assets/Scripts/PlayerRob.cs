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

    [Header("Rob Settings")]
    [SerializeField] private float robDistance = 3f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI Crosshair")]
    [SerializeField] private Image crosshair;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color canRobColor = Color.red;
    
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    
    private PlayerRob targetPlayer;
    private PlayerInputActions inputActions;
    private InputAction robAction;
    private PlayerInput playerInput;

    void Start()
    {
        // Si no se asignó la cámara, buscar la cámara principal
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        // Si aún es null, buscar en los hijos
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        
        // Si todavía es null, dar advertencia
        if (playerCamera == null)
            Debug.LogError($"No se encontró cámara para {gameObject.name}. Asigna una cámara en el Inspector.");
        
        // Configurar Input System
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            robAction = playerInput.actions["Rob"];
            
            // Suscribirse al evento cuando se presiona el botón de robo
            robAction.performed += OnRobPerformed;
        }
        else
        {
            Debug.LogError($"No se encontró PlayerInput en {gameObject.name}. Asegúrate de tener el componente PlayerInput.");
        }
        
        // Actualizar el estado visual de la corona
        UpdateCrownVisual(hasCrown.Value);
    }

    void OnDestroy()
    {
        // Desuscribirse del evento para evitar memory leaks
        if (robAction != null)
        {
            robAction.performed -= OnRobPerformed;
        }
    }

    void OnRobPerformed(InputAction.CallbackContext context)
    {
        // Solo el dueño puede robar
        if (!IsOwner) return;
        
        // Intentar robar si hay un objetivo válido
        if (targetPlayer != null)
        {
            RobCrown();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Suscribirse a cambios en la corona
        hasCrown.OnValueChanged += OnCrownChanged;
        
        // Aplicar el estado inicial
        UpdateCrownVisual(hasCrown.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        hasCrown.OnValueChanged -= OnCrownChanged;
    }

    void OnCrownChanged(bool oldValue, bool newValue)
    {
        UpdateCrownVisual(newValue);
        Debug.Log($"[{gameObject.name}] Corona cambiada: {oldValue} -> {newValue}");
    }

    void Update()
    {
        // Solo el dueño del jugador puede controlar el robo
        if (!IsOwner) return;

        if (hasCrown.Value)
        {
            // Si tengo corona, solo huir (no necesito detectar)
            if (crosshair != null)
                crosshair.color = normalColor;
            return;
        }
        
        // Si NO tengo corona, buscar jugadores con corona para robar
        DetectTargetPlayer();
    }
    
    void DetectTargetPlayer()
    {
        // Verificar que la cámara existe antes de usar
        if (playerCamera == null)
            return;
            
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        
        // Lanzar raycast desde el centro de la pantalla
        if (Physics.Raycast(ray, out hit, robDistance, playerLayer))
        {
            // Verificar la distancia - debe estar dentro del rango
            float distance = Vector3.Distance(transform.position, hit.point);
            
            if (distance <= robDistance)
            {
                PlayerRob player = hit.collider.GetComponent<PlayerRob>();
                
                // Verificar que sea un jugador válido y tenga corona
                if (player != null && player != this && player.HasCrown())
                {
                    targetPlayer = player;
                    
                    // Cambiar color del crosshair a "puede robar"
                    if (crosshair != null)
                        crosshair.color = canRobColor;
                    
                    return;
                }
            }
        }
        
        // No hay objetivo válido
        targetPlayer = null;
        if (crosshair != null)
            crosshair.color = normalColor;
    }

    void RobCrown()
    {
        if (targetPlayer == null) return;
        
        // Obtener el NetworkObjectId del jugador objetivo
        var targetNetObj = targetPlayer.GetComponent<NetworkObject>();
        if (targetNetObj != null)
        {
            // Llamar al servidor para robar la corona
            RobCrownServerRpc(targetNetObj.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RobCrownServerRpc(ulong targetNetworkObjectId)
    {
        // Buscar el jugador objetivo por NetworkObjectId
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            var targetPlayer = targetNetObj.GetComponent<PlayerRob>();
            if (targetPlayer != null && targetPlayer.hasCrown.Value)
            {
                // Transferir la corona
                targetPlayer.hasCrown.Value = false;
                this.hasCrown.Value = true;
                
                Debug.Log($"[Server] {gameObject.name} robó la corona de {targetPlayer.gameObject.name}!");
            }
        }
    }

    void UpdateCrownVisual(bool active)
    {
        if (crownObject != null)
        {
            crownObject.SetActive(active);
        }
    }

    // ===== MÉTODOS PÚBLICOS PARA EL CROWNMANAGER =====

    // Método directo para el servidor (NO es RPC, se llama directamente en el servidor)
    public void SetCrownDirect(bool value)
    {
        if (!IsServer)
        {
            Debug.LogWarning("SetCrownDirect debe llamarse solo desde el servidor!");
            return;
        }
        
        hasCrown.Value = value;
    }

    // Getter público
    public bool HasCrown()
    {
        return hasCrown.Value;
    }

    // Método de compatibilidad (solo servidor)
    public void SetCrown(bool value)
    {
        if (IsServer)
        {
            hasCrown.Value = value;
        }
        else
        {
            Debug.LogWarning("SetCrown() llamado desde cliente. Usa SetCrownServerRpc()");
        }
    }
    
    // Visualizar el rango de robo en el editor
    void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 direction = playerCamera.transform.forward;
            Gizmos.DrawRay(playerCamera.transform.position, direction * robDistance);
        }
    }
}