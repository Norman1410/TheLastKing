using UnityEngine;

public class DummyPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 3f;
    
    [Header("AI Settings (para Dummies)")]
    [SerializeField] private bool isAI = false; // Marcar true para NPCs
    [SerializeField] private float aiChangeDirectionTime = 2f;
    
    private CharacterController characterController;
    private float aiTimer = 0f;
    private Vector3 aiDirection;
    
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        
        if (characterController == null)
        {
            Debug.LogWarning("Se necesita un CharacterController! Añadiendo uno...");
            characterController = gameObject.AddComponent<CharacterController>();
        }
        
        if (isAI)
        {
            ChangeAIDirection();
        }
    }
    
    void Update()
    {
        if (isAI)
        {
            UpdateAI();
        }
        else
        {
            UpdatePlayerControl();
        }
    }
    
    void UpdatePlayerControl()
    {
        // Movimiento WASD
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (moveDirection.magnitude >= 0.1f)
        {
            // Calcular rotación hacia donde nos movemos
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            
            // Mover el personaje
            characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
        
        // Aplicar gravedad
        characterController.Move(Vector3.down * 9.81f * Time.deltaTime);
    }
    
    void UpdateAI()
    {
        aiTimer += Time.deltaTime;
        
        // Cambiar dirección cada cierto tiempo
        if (aiTimer >= aiChangeDirectionTime)
        {
            ChangeAIDirection();
            aiTimer = 0f;
        }
        
        // Mover en la dirección actual
        if (aiDirection.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(aiDirection.x, aiDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            
            characterController.Move(aiDirection * moveSpeed * Time.deltaTime);
        }
        
        // Aplicar gravedad
        characterController.Move(Vector3.down * 9.81f * Time.deltaTime);
    }
    
    void ChangeAIDirection()
    {
        // Dirección aleatoria
        float randomAngle = Random.Range(0f, 360f);
        aiDirection = new Vector3(
            Mathf.Sin(randomAngle * Mathf.Deg2Rad),
            0f,
            Mathf.Cos(randomAngle * Mathf.Deg2Rad)
        ).normalized;
    }
}