using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] public float walkSpeed = 5f;
    [SerializeField] public float runSpeed = 8f;
    [SerializeField] public float jumpHeight = 2f;
    [SerializeField] public float gravity = -9.81f;
    [SerializeField] public float gravityMultiplier = 1.0f;
    
    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f; // Reducido para mejor control
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;
    
    [Header("Camera Reference")]
    [SerializeField] private Camera playerCamera;
    
    [Header("Animation")]
    [SerializeField] private Animator animator; // Referencia al Animator
    [SerializeField] private bool useAnimations = true; // Toggle para activar/desactivar animaciones

    [SerializeField] private NetworkObject netObj;
    
    // Components
    private CharacterController controller;
    private PlayerInputActions inputActions;
    
    
    // Movement variables
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isRunning;
    private bool isMoving;
    private bool wasGrounded;
    private bool isJumping;
    
    // Camera rotation variables
    private float xRotation = 0f;
    private float yRotation = 0f;
    private Vector2 lookInput;
    
    private void Awake()
    {
        // Get components
        controller = GetComponent<CharacterController>();
        //netObj = GetComponent<NetworkObject>();
        
        // Si no se asignó un animator, intentar encontrarlo
        if (animator == null)
        {   
            Debug.LogWarning("Animator not assigned! Trying to find one in children.");
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }else {
            Debug.Log("Animator assigned via inspector.");
        }
        
        // Create and setup input actions
        inputActions = new PlayerInputActions();
        
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Inicializar la rotación Y con la rotación actual del jugador
        yRotation = transform.eulerAngles.y;
    }
    
    private void OnEnable()
    {
        // Enable input actions
        inputActions.Enable();
        
        // Subscribe to input events
        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Move.canceled += OnMove;
        
        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLook;
        
        inputActions.Player.Jump.performed += OnJump;
        
        inputActions.Player.Sprint.performed += OnSprint;
        inputActions.Player.Sprint.canceled += OnSprint;
    }
    
    private void OnDisable()
    {
        // Unsubscribe from input events
        inputActions.Player.Move.performed -= OnMove;
        inputActions.Player.Move.canceled -= OnMove;
        
        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLook;
        
        inputActions.Player.Jump.performed -= OnJump;
        
        inputActions.Player.Sprint.performed -= OnSprint;
        inputActions.Player.Sprint.canceled -= OnSprint;
        
        // Disable input actions
        inputActions.Disable();
    }
    
    private void Update()
    {
        // Perform ground check
        CheckGround();
        
        // Handle movement
        HandleMovement();
        
        // Handle camera rotation
        HandleMouseLook();
        
        // Update animations
        UpdateAnimations();
    }
    
    private void CheckGround()
    {
        // Check if we're grounded using a sphere cast
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else
        {
            // Alternativa: usar un raycast desde el centro del CharacterController
            isGrounded = Physics.Raycast(transform.position, Vector3.down, controller.bounds.extents.y + 0.1f, groundMask);
        }
        
        // Debug para ver el estado del ground check
        if (isGrounded != wasGrounded)
        {
            Debug.Log($"Ground State Changed: {isGrounded}");
            wasGrounded = isGrounded;
            
            // Si acabamos de aterrizar, ya no estamos saltando
            if (isGrounded)
            {
                isJumping = false;
            }
        }
        
        // Reset falling velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small negative value to keep grounded
            isJumping = false;
        }
    }
    
    private void HandleMovement()
{
    if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
        return;

    float currentSpeed = isRunning ? runSpeed : walkSpeed;
    Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

    // APLICACIÓN DE GRAVEDAD Y MOVIMIENTO LATERAL
    velocity.y += (gravity * gravityMultiplier) * Time.deltaTime; 
    
    if (isGrounded && velocity.y < 0)
    {
        velocity.y = -2f; 
    }
    
    isMoving = moveInput.magnitude > 0.1f;

    Vector3 finalMovement = (move * currentSpeed) + new Vector3(0f, velocity.y, 0f);
    controller.Move(finalMovement * Time.deltaTime);

}

    
    private void HandleMouseLook()
    {
        // Calculate rotation based on mouse input
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
        
        // Rotate player body on Y axis (horizontal rotation)
        yRotation += mouseX;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        
        // Rotate camera on X axis (vertical rotation)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        
        // Apply camera rotation
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
        else
        {
            Debug.LogWarning("Player Camera is not assigned! Please assign it in the inspector.");
        }
    }

    private void UpdateAnimations()
    {
        if (!useAnimations || animator == null) return;
        
        // Solo el jugador local actualiza animaciones
        if (!netObj.IsOwner) return;

        // Magnitud del movimiento (para el parámetro Speed)
        float currentSpeed = new Vector2(moveInput.x, moveInput.y).magnitude;

        // Dirección hacia adelante o atrás (para el parámetro Direction)
        float direction = moveInput.y;

        // Actualizar parámetros del Animator
        if (isRunning) animator.SetFloat("Speed", currentSpeed * 2f); // 0 → 2
        else animator.SetFloat("Speed", currentSpeed); // 0 → 1
        
        animator.SetFloat("Direction", direction);
        animator.SetBool("IsJumping", isJumping);
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsGrounded", isGrounded);


        //Debug.Log($"Speed: {currentSpeed}, Direction: {direction}"); //Está actualizando bien los parámetros
        Debug.Log($"Animator Parameters -->");
        if (animator.GetFloat("Speed") != 0) Debug.Log($" - Speed: {animator.GetFloat("Speed")}");
        if (animator.GetFloat("Direction") != 0) Debug.Log($" - Direction: {animator.GetFloat("Direction")}");
        //Debug.Log($" - IsJumping: {animator.GetBool("IsJumping")}");
        //Debug.Log($" - IsRunning: {animator.GetBool("IsRunning")}");
        //Debug.Log($" - IsGrounded: {animator.GetBool("IsGrounded")}");
    }


    
    // Input callback methods
    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    
    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }
    
    private void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log($"Jump pressed! IsGrounded: {isGrounded}, GroundCheck assigned: {groundCheck != null}");
        
        if (isGrounded && !isJumping)
        {
            // Calculate jump velocity using physics formula: v = sqrt(h * -2 * g)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            UpdateAnimations(); // Actualizar animaciones inmediatamente
            Debug.Log($"Jumping with velocity: {velocity.y}");
        }
    }
    
    private void OnSprint(InputAction.CallbackContext context)
    {
        isRunning = context.performed;
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        // Visualizar el GroundCheck
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        else if (controller != null)
        {
            // Si no hay groundCheck, mostrar el raycast alternativo
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 rayStart = transform.position;
            Vector3 rayEnd = rayStart + Vector3.down * (controller.bounds.extents.y + 0.1f);
            Gizmos.DrawLine(rayStart, rayEnd);
            Gizmos.DrawWireSphere(rayEnd, 0.1f);
        }
        


        // Visualizar la dirección de la cámara
        if (playerCamera != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * 2f);
        }
    }

    // Método público para permitir/bloquear el movimiento del mouse (útil para menús)
    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    //void OnGUI()
    //{
    //    // ESQUINA INFERIOR IZQUIERDA
    //    GUILayout.BeginArea(new Rect(10, Screen.height - 110, 300, 100));
//
    //    GUILayout.Label($"isWalking: {isMoving}");
    //    
    //    GUILayout.EndArea();
    //}
    
    // Métodos públicos para obtener el estado (útiles para otros scripts)
    public bool IsGrounded() => isGrounded;
    public bool IsRunning() => isRunning;
    public bool IsJumping() => isJumping;
    public bool IsWalking() => moveInput.magnitude > 0.1f && !isRunning;

    // ===============================================
    // NUEVO MÉTODO PÚBLICO PARA LA TRAMPA DE RESORTE
    // ===============================================
    /// <summary>
    /// Aplica una fuerza vertical al CharacterController. Usado por trampas.
    /// </summary>
    /// <param name="force">La velocidad inicial hacia arriba.</param>
    public void ApplyExternalLaunch(float force)
    {
        // 1. Sobrescribe la velocidad vertical actual con la fuerza de lanzamiento.
        velocity.y = force;

        // 2. Opcional: Establece isJumping a true para que las animaciones se actualicen.
        isJumping = true;

        // 3. Opcional: Restablecer isGrounded para que no se intente resetear la velocidad inmediatamente
        isGrounded = false;
        wasGrounded = false;

        Debug.Log($"External Launch Applied: {force}");
    }
    
    // Getter y Setter públicos para walkSpeed
    public float GetWalkSpeed()
    {
        return walkSpeed;
    }

    public void SetWalkSpeed(float newSpeed)
    {
        // Validar que no sea un valor negativo ni cero
        if (newSpeed < 0)
        {
            Debug.LogWarning("El valor de walkSpeed no puede ser negativo. Se mantendrá el valor anterior.");
            return;
        }

        walkSpeed = newSpeed;
    }
}