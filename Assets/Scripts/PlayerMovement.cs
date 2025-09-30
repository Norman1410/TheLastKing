using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleFirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    
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
    
    // Components
    private CharacterController controller;
    private PlayerInputActions inputActions;
    
    // Movement variables
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isRunning;
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
        
        // Si no se asignó un animator, intentar encontrarlo
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
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
        // Calculate movement direction based on input
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        
        // Apply movement
        controller.Move(move * currentSpeed * Time.deltaTime);
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
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
        
        // Calcular si el personaje se está moviendo
        bool isMoving = moveInput.magnitude > 0.1f;
        
        // Actualizar parámetros del Animator
        animator.SetBool("IsWalking", isMoving && !isRunning);
        animator.SetBool("IsRunning", isMoving && isRunning);
        animator.SetBool("IsJumping", isJumping);
        
        // Opcional: también puedes enviar la velocidad como un float para blend trees
        float speed = isMoving ? (isRunning ? runSpeed : walkSpeed) : 0f;
        animator.SetFloat("Speed", speed);
        
        // Opcional: enviar el estado de grounded
        animator.SetBool("IsGrounded", isGrounded);
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
    
    // Métodos públicos para obtener el estado (útiles para otros scripts)
    public bool IsGrounded() => isGrounded;
    public bool IsRunning() => isRunning;
    public bool IsJumping() => isJumping;
    public bool IsWalking() => moveInput.magnitude > 0.1f && !isRunning;
}