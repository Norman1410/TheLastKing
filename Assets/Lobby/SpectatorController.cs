using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CameraAnchor))]
public class SpectatorController : NetworkBehaviour
{
    [Header("Pivots")]
    [SerializeField] private Transform cameraPivot; // empty child for camera

    [Header("Move")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float fastMultiplier = 2.0f;
    [SerializeField] private float verticalSpeed = 8f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 1.2f;

    [Header("Bounds")]
    [SerializeField] private bool clampToBox = true;
    [SerializeField] private Vector3 minBounds = new(-120, 10, -120);
    [SerializeField] private Vector3 maxBounds = new(120, 120, 120);

    private bool _enabled;
    private float yaw, pitch;
    private CameraAnchor _anchor;

    void Awake() { _anchor = GetComponent<CameraAnchor>(); }

    public void EnableSpectatorLocal(bool enable)
    {
        _enabled = enable;
        _anchor.SetActiveLocal(enable);
        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    void Update()
    {
        if (!IsOwner || !_enabled) return;

        // Look
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0);

        // Move
        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 world = cameraPivot.TransformDirection(input);
        world.y = 0f;
        if (Input.GetKey(KeyCode.E)) world.y += verticalSpeed;
        if (Input.GetKey(KeyCode.Q)) world.y -= verticalSpeed;
        transform.position += world * speed * Time.deltaTime;

        if (clampToBox)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minBounds.x, maxBounds.x);
            p.y = Mathf.Clamp(p.y, minBounds.y, maxBounds.y);
            p.z = Mathf.Clamp(p.z, minBounds.z, maxBounds.z);
            transform.position = p;
        }
    }
}
