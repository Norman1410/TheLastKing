using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : Unity.Netcode.NetworkBehaviour
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
    [SerializeField] private Transform visualsRoot; // Root que contiene los meshes/visuals (asignar en el prefab)



    // Componentes
    private CharacterController controller;
    private PlayerInputActions inputActions;
    // Animator/network
    // Animator / red
    //private PlayerAnimatorSync animatorSync;


    // Variables de movimiento
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isRunning;
    private bool isMoving;
    private bool wasGrounded;
    private bool isJumping;

    // Variables de rotación de la cámara
    private float xRotation = 0f;
    private float yRotation = 0f;
    private Vector2 lookInput;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        // Nota: la propiedad de red y la suscripción a entradas se gestionan en OnNetworkSpawn/OnNetworkDespawn.

        // Si estamos usando Netcode y hay un Animator, añadir NetworkAnimator para sincronizar parámetros
        if (Unity.Netcode.NetworkManager.Singleton != null && animator != null)
        {
            var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
            if (netAnim == null)
                netAnim = gameObject.AddComponent<Unity.Netcode.Components.NetworkAnimator>();

            // Asegurar que el NetworkAnimator apunta al Animator correcto (útil si el Animator está en un hijo)
            if (netAnim != null && netAnim.Animator == null)
            {
                netAnim.Animator = animator;
            }
        }

        // Obtener componentes
        controller = GetComponent<CharacterController>();

        // Si no se asignó un animator, intentar encontrarlo
        if (animator == null)
        {
            Debug.LogWarning("Animator not assigned! Trying to find one in children.");
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }
        else
        {
            Debug.Log("Animator assigned via inspector.");
        }

        // Crear y configurar las acciones de entrada
        inputActions = new PlayerInputActions();

        // Bloquear el cursor en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Inicializar la rotación Y con la rotación actual del jugador
        yRotation = transform.eulerAngles.y;

        // Ajustar visibilidad del modelo para el propietario local
        //UpdateLocalModelVisibility();
    }

    private void OnEnable()
    {
        // Si no se ejecuta con Netcode (modo single-player/Editor sin NetworkManager), habilitar entradas inmediatamente.
        if (Unity.Netcode.NetworkManager.Singleton == null)
        {
            inputActions.Enable();
            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;
            inputActions.Player.Look.performed += OnLook;
            inputActions.Player.Look.canceled += OnLook;
            inputActions.Player.Jump.performed += OnJump;
            inputActions.Player.Sprint.performed += OnSprint;
            inputActions.Player.Sprint.canceled += OnSprint;
        }
        // En caso contrario, la suscripción de entradas se realiza en OnNetworkSpawn() cuando se conozca la propiedad.
    }


    private void OnDisable()
    {
        // Si es single-player o no hay Netcode, anular suscripción aquí
        if (Unity.Netcode.NetworkManager.Singleton == null)
        {
            inputActions.Player.Move.performed -= OnMove;
            inputActions.Player.Move.canceled -= OnMove;
            inputActions.Player.Look.performed -= OnLook;
            inputActions.Player.Look.canceled -= OnLook;
            inputActions.Player.Jump.performed -= OnJump;
            inputActions.Player.Sprint.performed -= OnSprint;
            inputActions.Player.Sprint.canceled -= OnSprint;
            inputActions.Disable();
        }
        // De lo contrario, OnNetworkDespawn se encarga de anular la suscripción.
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            inputActions.Enable();
            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;
            inputActions.Player.Look.performed += OnLook;
            inputActions.Player.Look.canceled += OnLook;
            inputActions.Player.Jump.performed += OnJump;
            inputActions.Player.Sprint.performed += OnSprint;
            inputActions.Player.Sprint.canceled += OnSprint;

            // 🔵 Inicializar PowerManager para jugador local
            var powerManager = GetComponentInChildren<PowerManager>();
            if (powerManager != null)
                powerManager.InitializeForLocalPlayer(this);
            else
                Debug.LogWarning("[FirstPersonController] No se encontró PowerManager en hijos.");
        }

        UpdateLocalModelVisibility();
    }


    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner)
        {
            inputActions.Player.Move.performed -= OnMove;
            inputActions.Player.Move.canceled -= OnMove;
            inputActions.Player.Look.performed -= OnLook;
            inputActions.Player.Look.canceled -= OnLook;
            inputActions.Player.Jump.performed -= OnJump;
            inputActions.Player.Sprint.performed -= OnSprint;
            inputActions.Player.Sprint.canceled -= OnSprint;
            inputActions.Disable();
        }
    }

    private void Update()
    {
        // Realizar comprobación de suelo
        CheckGround();

        // Gestionar movimiento
        HandleMovement();

        // Gestionar la rotación de la cámara
        HandleMouseLook();

        // Actualizar animaciones
        UpdateAnimations();
    }

    private void CheckGround()
    {
        // Comprobar si estamos en el suelo usando una esfera
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else
        {
            // Alternativa: usar un raycast desde el centro del CharacterController
            isGrounded = Physics.Raycast(transform.position, Vector3.down, controller.bounds.extents.y + 0.1f, groundMask);
        }

        // Depuración para ver el estado de la comprobación de suelo
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

        // Reiniciar la velocidad de caída cuando estemos en el suelo
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
        // Calcular la rotación a partir de la entrada del ratón
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Rotar el cuerpo del jugador en el eje Y (rotación horizontal)
        yRotation += mouseX;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        // Rotar la cámara en el eje X (rotación vertical)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        // Aplicar la rotación de la cámara
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

        // Calcular los parámetros de animación a partir del estado de movimiento local
        float currentSpeed = new Vector2(moveInput.x, moveInput.y).magnitude;
        float forward = moveInput.y;
        float strafe = moveInput.x;

        Debug.Log($"Speed: {currentSpeed}, Forward: {forward}, Strafe: {strafe}"); // Está actualizando bien los parámetros

        // En red: sólo el propietario debe controlar y establecer los parámetros del animator y enviar el estado
        if (IsOwner)
        {
            if (isRunning) animator.SetFloat("Speed", currentSpeed * 2f);
            else animator.SetFloat("Speed", currentSpeed);

            // Forward/backward and left/right strafing parameters
            animator.SetFloat("Direction", forward);
            animator.SetFloat("Strafe", strafe);
            animator.SetBool("IsJumping", isJumping);
            animator.SetBool("IsRunning", isRunning);
            animator.SetBool("IsGrounded", isGrounded);

            // Enviar el estado al servidor para que lo difunda a otros clientes
            if (AnimationNetworkManager.Instance != null && AnimationNetworkManager.Instance.IsSpawned)
            {
                AnimationNetworkManager.Instance.SubmitAnimationStateServerRpc(currentSpeed, forward, strafe, isJumping, isRunning, isGrounded);
            }
        }

        //Debug.Log($"Animator Parameters -->");
        //if (animator.GetFloat("Speed") != 0) Debug.Log($" - Speed: {animator.GetFloat("Speed")}");
        //if (animator.GetFloat("Direction") != 0) Debug.Log($" - Direction: {animator.GetFloat("Direction")}");
        //Debug.Log($" - IsJumping: {animator.GetBool("IsJumping")}");
        //Debug.Log($" - IsRunning: {animator.GetBool("IsRunning")}");
        //Debug.Log($" - IsGrounded: {animator.GetBool("IsGrounded")}");
    }



    // Métodos de callback de entrada
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
            // Calcular la velocidad de salto usando la fórmula física: v = sqrt(h * -2 * g)
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

    // Visualización de depuración
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

    // Método público para permitir/bloquear el movimiento del ratón (útil para menús)
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

    /// <summary>
    /// Llamado por el gestor de red cuando se difunde el estado de animación de un jugador remoto.
    /// Aplica los parámetros de animación recibidos al Animator local de ese jugador remoto.
    /// </summary>
    public void ApplyRemoteAnimationState(float speed, float forward, float strafe, bool isJumping, bool isRunning, bool isGrounded)
    {
        if (animator == null || !useAnimations) return;

        // Apply exactly the state received from network
        if (isRunning) animator.SetFloat("Speed", speed * 2f);
        else animator.SetFloat("Speed", speed);

        // Forward/backward and left/right strafing parameters
        animator.SetFloat("Direction", forward);
        animator.SetFloat("Strafe", strafe);
        animator.SetBool("IsJumping", isJumping);
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsGrounded", isGrounded);
    }

    /// <summary>
    /// Oculta los meshes visibles del modelo del jugador local para que el propietario no vea su propio cuerpo en primera persona.
    /// Esto desactiva los componentes Renderer en objetos hijo, pero deja intactos los hijos de la cámara.
    /// </summary>
    private void UpdateLocalModelVisibility()
    {
        // If not networked, don't hide by default (single-player)
        if (Unity.Netcode.NetworkManager.Singleton == null) return;

        bool hide = IsOwner;

        // Target a specific visuals root if provided; otherwise operate on this transform
        var targetRoot = visualsRoot != null ? visualsRoot : transform;

        // Disable renderers for the local owner to avoid clipping into the camera
        var renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // Skip renderers that are part of the player camera hierarchy
            if (playerCamera != null && r.transform.IsChildOf(playerCamera.transform))
                continue;

            r.enabled = !hide;
        }

        // Additionally, toggle CanvasRenderer gameObjects if present under the visuals root
        var canvasRenderers = targetRoot.GetComponentsInChildren<CanvasRenderer>(true);
        foreach (var cr in canvasRenderers)
        {
            // NO ocultar HUDs (PowersHUD, CrownHUD, CrosshairUI)
            if (cr.gameObject.name.Contains("HUD") ||
                cr.gameObject.name.Contains("Power") ||
                cr.gameObject.name.Contains("Crosshair") ||
                cr.gameObject.name.Contains("Crown"))
                continue;

            cr.gameObject.SetActive(!hide);
        }

    }
}