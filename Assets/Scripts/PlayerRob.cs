using UnityEngine;

public class PlayerRob : MonoBehaviour
{
    public GameObject crown; // referencia a la corona de este jugador
    public float robDistance = 2f; // distancia máxima para robar la corona
    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        if (inputActions.Player.Rob.WasPressedThisFrame())
        {
            TryRob();
        }
    }

    void TryRob()
    {
        Camera cam = Camera.main;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, robDistance)) // rango de robo = 2 metros
        {
            PlayerRob otherPlayer = hit.collider.GetComponentInParent<PlayerRob>();
            if (otherPlayer != null && otherPlayer.HasCrown())
            {
                otherPlayer.SetCrown(false);
                SetCrown(true);
                Debug.Log("Corona robada!");
            }
        }
    }

    public bool HasCrown()
    {
        return crown.activeSelf;
    }

    public void SetCrown(bool value)
    {
        crown.SetActive(value);
    }
}

