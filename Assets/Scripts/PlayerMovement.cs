using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Range(0, 100)] public float mouseSensitivity = 25f;
    [Range(0f, 200f)] private float snappiness = 100f;
    [Range(0f, 20f)] public float walkSpeed = 10f;
    [Range(0f, 30f)] public float sprintSpeed = 15f;
    [Range(0f, 10f)] public float crouchSpeed = 6f;
    [Range(0f, 15f)] public float jumpSpeed = 3f;
    [Range(0f, 50f)] public float gravity = 9.81f;
    public bool coyoteTimeEnabled = true;
    public float coyoteTimeDuration = 0.25f;
    public float normalFov = 60f;
    public bool canJump = true;
    public bool canSprint = true;
    public Transform groundCheck;
    public float groundDistance = 0.3f;
    public LayerMask groundMask;
    public Transform playerCamera;
    public Transform cameraParent;
    private float rotX, rotY;
    private float xVelocity, yVelocity;
    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private bool isGrounded;
    private Vector2 moveInput;
    public bool isSprinting;
    public bool isCrouching;
    public bool isSliding;
    private float originalHeight;
    private float originalCameraParentHeight;
    private float coyoteTimer;
    private Camera cam;
    private float defaultPosY;
    private bool isLook = true, isMove = true;
    private float currentCameraHeight;
    private float currentFov;
    
    private PlayerInputActions inputActions;

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        cam = playerCamera.GetComponent<Camera>();
        originalHeight = characterController.height;
        originalCameraParentHeight = cameraParent.localPosition.y;
        defaultPosY = cameraParent.localPosition.y;

        inputActions = new PlayerInputActions(); // NUEVO

        Cursor.lockState = CursorLockMode.Locked;
        currentCameraHeight = originalCameraParentHeight;
        currentFov = normalFov;
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        if (isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = -2f;
            coyoteTimer = coyoteTimeEnabled ? coyoteTimeDuration : 0f;
        }
        else if (coyoteTimeEnabled)
        {
            coyoteTimer -= Time.deltaTime;
        }

        HandleLook();
        HandleMovement();
    }

    // -------------------
    // Mouse look
    // -------------------
    private void HandleLook()
    {
        if (!isLook) return;

        Vector2 lookInput = inputActions.Player.Look.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        rotX += mouseX;
        rotY -= mouseY;
        rotY = Mathf.Clamp(rotY, -90f, 90f);

        xVelocity = Mathf.Lerp(xVelocity, rotX, snappiness * Time.deltaTime);
        yVelocity = Mathf.Lerp(yVelocity, rotY, snappiness * Time.deltaTime);

        playerCamera.localRotation = Quaternion.Euler(yVelocity, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, xVelocity, 0f);
    }

    // -------------------
    // Movimiento, sprint y salto
    // -------------------
    private void HandleMovement()
    {
        Vector2 moveInputRaw = inputActions.Player.Move.ReadValue<Vector2>();
        moveInput = moveInputRaw.normalized;

        bool sprintInput = inputActions.Player.Sprint.IsPressed();
        //if (sprintInput) Debug.Log("Sprint input detected");
        isSprinting = canSprint && sprintInput && moveInput.y > 0.1f && isGrounded && !isCrouching && !isSliding;

        float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        if (!isMove) currentSpeed = 0f;

        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 moveVector = transform.TransformDirection(direction) * currentSpeed;

        Debug.Log("isGrounded: " + isGrounded);

        if (isGrounded || coyoteTimer > 0f)
        {
            if (canJump && inputActions.Player.Jump.WasPressedThisFrame() && !isSliding)
            {
                moveDirection.y = jumpSpeed;
                Debug.Log("Jump!");
            }
            else if (moveDirection.y < 0)
            {
                moveDirection.y = -2f;
            }
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        if (!isSliding)
        {
            moveDirection = new Vector3(moveVector.x, moveDirection.y, moveVector.z);
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }
    
}