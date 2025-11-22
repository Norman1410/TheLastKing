using System.Collections;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
/// <summary>
/// Finds a TextMeshPro child (nicknametext) and sets it to the player's display name.
/// It prefers the name from LanLobbyState.Players (server-synced). Falls back to PlayerName.Get().
/// Attach this to the Player prefab.
/// The component will also subscribe to LanLobbyState.Players.OnListChanged to update if the server changes the name.
/// </summary>
public class PlayerNicknameDisplay : MonoBehaviour
{
    [Tooltip("Optional reference to the TMP_Text (nicknametext). If empty, it will search children.")]
    public TMP_Text nicknameText;

    NetworkObject _netObj;
    PlayerNetworkDisplayName _netName;

    void OnDestroy()
    {
        var st = LanLobbyState.Instance;
        if (st != null && st.Players != null)
        {
            try { st.Players.OnListChanged -= OnPlayersListChanged; } catch { }
        }
    }

    IEnumerator Start()
    {
        // Try to find TMP if not assigned
        if (nicknameText == null)
            nicknameText = FindNickTextInChildren();

        if (nicknameText == null)
        {
            Debug.LogWarning("[PlayerNicknameDisplay] No TMP_Text found in children to show nickname.");
            yield break;
        }

        // Wait until NetworkManager exists (or proceed immediately for single-player testing)
        yield return new WaitUntil(() => NetworkManager.Singleton != null);

        // Try to find a NetworkObject on this GO or any parent (some prefabs attach the TMP to a child)
        _netObj = GetComponent<NetworkObject>() ?? GetComponentInParent<NetworkObject>();

        // If we have a NetworkObject, wait until it's spawned so OwnerClientId is valid
        if (_netObj != null)
            yield return new WaitUntil(() => _netObj.IsSpawned);
        else
        {
            // If there's no NetworkObject in parent chain, try to find one later when scene stabilizes
            Debug.LogWarning($"[PlayerNicknameDisplay] NetworkObject not found on {gameObject.name} or parents. OwnerId fallback will use LocalClientId.");
        }

        // Subscribe to list changes so we can update if the server sends a new display name
        var st = LanLobbyState.Instance;
        if (st != null && st.Players != null)
        {
            st.Players.OnListChanged += OnPlayersListChanged;
        }

        // Try to get PlayerNetworkDisplayName on the player root (if any)
        if (_netObj != null)
        {
            _netName = _netObj.GetComponent<PlayerNetworkDisplayName>();
            if (_netName != null)
            {
                // Subscribe to networked name changes
                _netName.DisplayName.OnValueChanged += (_, __) => UpdateName();
            }
        }
        else
        {
            // Try to find any PlayerNetworkDisplayName in parent chain as last resort
            _netName = GetComponentInParent<PlayerNetworkDisplayName>();
            if (_netName != null)
                _netName.DisplayName.OnValueChanged += (_, __) => UpdateName();
        }

        UpdateName();
    }

    void OnPlayersListChanged(Unity.Netcode.NetworkListEvent<LanPlayerEntry> ev)
    {
        // Whenever the list changes, recompute name for this player
        UpdateName();
    }

    void UpdateName()
    {
    if (nicknameText == null) return;

    string display = PlayerName.Get(); // fallback

        // Prefer a networked DisplayName component on the player object if available
        if (_netName != null)
        {
            var v = _netName.DisplayName.Value;
            if (!v.IsEmpty)
            {
                display = v.ToString();
                nicknameText.text = display;
                Debug.Log($"[PlayerNicknameDisplay] Usando DisplayName en red='{display}' en {gameObject.name} (OwnerId={(_netObj!=null?_netObj.OwnerClientId:0)})");
                return;
            }
        }

        // Next preferred source: LanLobbyState (server-synced)
        // Determinar ownerId de forma robusta. Evitar usar LocalClientId por defecto porque
        // si _netObj es null podríamos mostrar el nombre local para todos los objetos.
        ulong ownerId = ulong.MaxValue; // invalid sentinel

        if (_netObj != null)
        {
            ownerId = _netObj.OwnerClientId;
        }
        else
        {
            // Intentar encontrar un NetworkObject relacionado (padre/ancestro/descendiente) como fallback
            var all = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            foreach (var no in all)
            {
                if (no == null) continue;
                // Si este transform es hijo del NetworkObject o viceversa, considerarlo relacionado
                if (transform.IsChildOf(no.transform) || no.transform.IsChildOf(transform))
                {
                    ownerId = no.OwnerClientId;
                    _netObj = no; // cachearlo para futuras actualizaciones
                    _netName = no.GetComponent<PlayerNetworkDisplayName>() ?? _netName;
                    break;
                }
            }
        }

        var st = LanLobbyState.Instance;
        if (st != null && st.Players != null && ownerId != ulong.MaxValue)
        {
            for (int i = 0; i < st.Players.Count; i++)
            {
                if (st.Players[i].ClientId == ownerId)
                {
                    display = st.Players[i].Name.ToString();
                    break;
                }
            }
        }

        nicknameText.text = display;

        // debug info: qué ownerId se usó y qué display fue elegido
        Debug.Log($"[PlayerNicknameDisplay] nickname actualizado en {gameObject.name}: ownerId={(ownerId==ulong.MaxValue? -1: (long)ownerId)}, display='{display}', hasNetObj={_netObj!=null}");
    }

    TMP_Text FindNickTextInChildren()
    {
        // Prefer an object explicitly named "nicknametext" (case-insensitive)
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLower().Contains("nick"))
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
        }

        // Otherwise return the first TMP_Text found in children
        return GetComponentInChildren<TMP_Text>(true);
    }
}
