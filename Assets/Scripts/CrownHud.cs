using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Actualiza la imagen del HUD mostrando si el jugador local tiene o no tiene la corona
/// Se suscribe al evento OnCrownStatusChanged del PlayerRob local (IsOwner).
/// </summary>
public class CrownHud : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("Imagen UI que se actualiza según el estado de la corona")]
    public Image crownImage;

    [Header("Crown Sprites")]
    [Tooltip("Sprite cuando el jugador TIENE la corona")]
    public Sprite crownHeldSprite;
    
    [Tooltip("Sprite cuando el jugador NO TIENE la corona")]
    public Sprite crownNotHeldSprite;

    private PlayerRob _localPlayerRob;

    void Start()
    {
        // Esperar a que el NetworkManager esté listo y encontrar el PlayerRob local
        StartCoroutine(FindLocalPlayerRob());
    }

    void OnDestroy()
    {
        // Desuscribirse del evento cuando se destruya el HUD
        if (_localPlayerRob != null)
        {
            _localPlayerRob.OnCrownStatusChanged -= OnCrownStatusChanged;
        }
    }

    System.Collections.IEnumerator FindLocalPlayerRob()
    {
        // Esperar a que NetworkManager exista
        yield return new WaitUntil(() => NetworkManager.Singleton != null);

        // Esperar a que el jugador local esté spawneado
        yield return new WaitUntil(() => NetworkManager.Singleton.LocalClient != null && 
                                         NetworkManager.Singleton.LocalClient.PlayerObject != null);

        // Obtener el PlayerRob del jugador local
        var localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
        _localPlayerRob = localPlayerObject.GetComponent<PlayerRob>();

        if (_localPlayerRob != null)
        {
            Debug.Log("[CrownHud] PlayerRob local encontrado. Suscribiéndose a evento de corona.");
            
            // Suscribirse al evento de cambio de corona
            _localPlayerRob.OnCrownStatusChanged += OnCrownStatusChanged;
            
            // Establecer estado inicial
            UpdateCrownImage(_localPlayerRob.HasCrown());
        }
        else
        {
            Debug.LogWarning("[CrownHud] No se encontró PlayerRob en el jugador local.");
        }
    }

    void OnCrownStatusChanged(bool hasCrown)
    {
        Debug.Log($"[CrownHud] Estado de corona cambió: {hasCrown}");
        UpdateCrownImage(hasCrown);
    }

    void UpdateCrownImage(bool hasCrown)
    {
        if (crownImage == null)
        {
            Debug.LogWarning("[CrownHud] crownImage no está asignada en el Inspector.");
            return;
        }

        if (hasCrown)
        {
            if (crownHeldSprite != null)
            {
                crownImage.sprite = crownHeldSprite;
                Debug.Log("[CrownHud] Imagen actualizada a: TIENE CORONA");
            }
            else
            {
                Debug.LogWarning("[CrownHud] crownHeldSprite no está asignado.");
            }
        }
        else
        {
            if (crownNotHeldSprite != null)
            {
                crownImage.sprite = crownNotHeldSprite;
                Debug.Log("[CrownHud] Imagen actualizada a: NO TIENE CORONA");
            }
            else
            {
                Debug.LogWarning("[CrownHud] crownNotHeldSprite no está asignado.");
            }
        }
    }
}
