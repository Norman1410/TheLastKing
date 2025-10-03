using UnityEngine;
using UnityEngine.UI;

public class PlayerRob : MonoBehaviour
{
    [Header("Corona Settings")]
    [SerializeField] private GameObject crownObject; // Asigna la corona del prefab aquí
    [SerializeField] private bool hasCrown = false;
    
    [Header("Rob Settings")]
    [SerializeField] private float robDistance = 3f; // Distancia para robar
    [SerializeField] private KeyCode robKey = KeyCode.Mouse0; // Click izquierdo
    [SerializeField] private LayerMask playerLayer; // Layer de los jugadores
    
    [Header("UI Crosshair")]
    [SerializeField] private Image crosshair; // Referencia al crosshair UI
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color canRobColor = Color.red;
    
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    
    private PlayerRob targetPlayer; // Jugador al que podemos robarle
    
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
            Debug.LogError($"No se encontró cámara para {gameObject.name}. Asigna una cámara en el Inspector o añade una cámara como hijo del jugador.");
        
        // Actualizar el estado visual de la corona
        UpdateCrownVisibility();
    }
    
    void Update()
    {
        if (hasCrown)
        {
            // Si tengo corona, solo huir (no necesito detectar)
            if (crosshair != null)
                crosshair.color = normalColor;
            return;
        }
        
        // Si NO tengo corona, buscar jugadores con corona para robar
        DetectTargetPlayer();
        
        // Intentar robar si presionamos el botón
        if (Input.GetKeyDown(robKey) && targetPlayer != null)
        {
            RobCrown();
        }
    }
    
    void DetectTargetPlayer()
    {
        // Verificar que la cámara existe antes de usar
        if (playerCamera == null)
            return;
            
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        
        // Lanzar raycast desde el centro de la pantalla
        // IMPORTANTE: Detecta cualquier cosa dentro del rango (0 a robDistance)
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
        
        // Robar la corona
        targetPlayer.LoseCrown();
        GainCrown();
        
        Debug.Log($"{gameObject.name} robó la corona de {targetPlayer.gameObject.name}!");
    }
    
    public void GainCrown()
    {
        hasCrown = true;
        UpdateCrownVisibility();
    }
    
    public void LoseCrown()
    {
        hasCrown = false;
        UpdateCrownVisibility();
    }
    
    public bool HasCrown()
    {
        return hasCrown;
    }
    
    public void SetCrown(bool value)
    {
        hasCrown = value;
        UpdateCrownVisibility();
    }
    
    private void UpdateCrownVisibility()
    {
        if (crownObject != null)
            crownObject.SetActive(hasCrown);
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