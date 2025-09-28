using System.Collections;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class RegisterAndBringUpHarness : MonoBehaviour
{
    static GameObject _playerPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootEarly()
    {
        // Asegurar NetworkManager + Transport una sola vez
        if (FindObjectOfType<NetworkManager>() == null)
        {
            var go = new GameObject("NetworkManager_BOOT");
            DontDestroyOnLoad(go);
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            utp.SetConnectionData("127.0.0.1", 7777);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // UI mínima
        if (FindObjectOfType<QuickJoinLocalUI>() == null)
        {
            var ui = new GameObject("MP_HARNESS_UI");
            ui.AddComponent<QuickJoinLocalUI>();
            DontDestroyOnLoad(ui);
        }

        // Apaga Main Camera de escena si no es de red
        var main = Camera.main;
        if (main != null && main.transform.GetComponentInParent<NetworkObject>() == null)
            main.gameObject.SetActive(false);

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Cargar y registrar PlayerPrefab
        _playerPrefab = Resources.Load<GameObject>("PlayerNetwork");
        if (_playerPrefab == null) { Debug.LogError("[Harness] Falta Resources/PlayerNetwork.prefab"); return; }
        if (nm.NetworkConfig.PlayerPrefab == null)
            nm.NetworkConfig.PlayerPrefab = _playerPrefab;
        try { nm.AddNetworkPrefab(_playerPrefab); } catch { }

        // ConnectionApproval para que el server cree el Player
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = OnApproval;

        // Hooks server para garantizar spawn + ownership correcto
        nm.OnServerStarted += () =>
        {
            if (nm.IsHost) nm.StartCoroutine(EnsurePlayerCo(nm.LocalClientId));
        };
        nm.OnClientConnectedCallback += clientId =>
        {
            if (!nm.IsServer) return;
            nm.StartCoroutine(EnsurePlayerCo(clientId));
        };
    }

    static void OnApproval(NetworkManager.ConnectionApprovalRequest req,
                           NetworkManager.ConnectionApprovalResponse resp)
    {
        resp.Approved = true;
        resp.CreatePlayerObject = true;

        // Hash del prefab (compatible varias versiones)
        uint hash = 0;
        var no = NetworkManager.Singleton.NetworkConfig.PlayerPrefab?.GetComponent<NetworkObject>();
        if (no != null)
        {
            var t = no.GetType();
            var pGlobal = t.GetProperty("GlobalObjectIdHash", BindingFlags.Public | BindingFlags.Instance);
            var pPrefab = t.GetProperty("PrefabIdHash",       BindingFlags.Public | BindingFlags.Instance);
            var p = pGlobal ?? pPrefab;
            if (p != null)
            {
                var val = p.GetValue(no);
                if (val is uint u) hash = u;
                else if (val is int i) hash = unchecked((uint)i);
                else if (val is ulong ul) hash = unchecked((uint)ul);
                else if (val is long l) hash = unchecked((uint)l);
            }
        }
        resp.PlayerPrefabHash = hash;
        resp.Position = Vector3.zero;
        resp.Rotation = Quaternion.identity;
    }

    static IEnumerator EnsurePlayerCo(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        // Espera 1–2 frames para dejar que NGO haga su spawn automático
        yield return null; yield return null;

        var existing = nm.SpawnManager.GetPlayerNetworkObject(clientId);
        if (existing == null)
        {
            // Crear a mano si no existe
            var go = Instantiate(_playerPrefab);
            var no = go.GetComponent<NetworkObject>();
            if (no == null) { Debug.LogError("[Harness] Prefab sin NetworkObject"); yield break; }
            no.SpawnAsPlayerObject(clientId);
            existing = no;
        }

        // Si por error quedó con dueño incorrecto, transfiere ownership
        if (existing.OwnerClientId != clientId)
            existing.ChangeOwnership(clientId);
    }
}
