using UnityEngine;

/// <summary>
/// Re-ancla la MainCamera al pivot de jugador o al pivot de espectador,
/// y expone SetActiveLocal(bool) para integrarse con SpectatorController.
/// </summary>
public class CameraAnchor : MonoBehaviour
{
    [Header("Pivots (asigna en el prefab del Player)")]
    public Transform playerPivot;     // Ej.: hijo "PlayerPivot" (altura de ojos)
    public Transform spectatorPivot;  // Ej.: hijo "SpectatorPivot" (vista aérea)

    [Header("Opcional")]
    [Tooltip("Si no hay MainCamera en la escena, crea una en runtime.")]
    public bool createCameraIfMissing = true;

    [Tooltip("Bloquea el cursor cuando el espectador está activo.")]
    public bool lockCursorWhenActive = true;

    Camera _mainCam;
    bool _activeLocal = false;

    void Awake()
    {
        EnsureMainCamera();
    }

    void EnsureMainCamera()
    {
        if (_mainCam != null) return;

        _mainCam = Camera.main;
        if (_mainCam == null && createCameraIfMissing)
        {
            var go = new GameObject("MainCamera");
            _mainCam = go.AddComponent<Camera>();
            _mainCam.tag = "MainCamera";
            _mainCam.nearClipPlane = 0.05f;
            _mainCam.farClipPlane  = 1000f;
        }
    }

    /// <summary>
    /// Activa/Desactiva el anclaje local al modo espectador (true) o modo jugador (false).
    /// Llamado típicamente por SpectatorController.
    /// </summary>
    public void SetActiveLocal(bool active)
    {
        _activeLocal = active;
        if (active) SwapToSpectatorPivot();
        else        SwapToPlayerPivot();

        if (lockCursorWhenActive)
        {
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !active;
        }
    }

    /// <summary>
    /// Ancla la MainCamera al pivot de jugador.
    /// </summary>
    public void SwapToPlayerPivot()
    {
        EnsureMainCamera();
        if (_mainCam == null)
        {
            Debug.LogWarning("[CameraAnchor] No hay MainCamera disponible para anclar (PlayerPivot).");
            return;
        }
        if (playerPivot == null)
        {
            Debug.LogWarning("[CameraAnchor] playerPivot no asignado.");
            return;
        }

        AttachToPivot(_mainCam.transform, playerPivot);
    }

    /// <summary>
    /// Ancla la MainCamera al pivot de espectador.
    /// </summary>
    public void SwapToSpectatorPivot()
    {
        EnsureMainCamera();
        if (_mainCam == null)
        {
            Debug.LogWarning("[CameraAnchor] No hay MainCamera disponible para anclar (SpectatorPivot).");
            return;
        }
        if (spectatorPivot == null)
        {
            Debug.LogWarning("[CameraAnchor] spectatorPivot no asignado.");
            return;
        }

        AttachToPivot(_mainCam.transform, spectatorPivot);
    }

    void AttachToPivot(Transform cam, Transform pivot)
    {
        cam.SetParent(pivot, worldPositionStays: false);
        cam.localPosition = Vector3.zero;
        cam.localRotation = Quaternion.identity;
    }
}
