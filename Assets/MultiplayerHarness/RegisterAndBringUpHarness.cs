using System.Collections;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class RegisterAndBringUpHarness : MonoBehaviour
{
    // Por defecto APAGADO: no interfiere con el lobby LAN ni con Relay.
    // Si quieres el flujo rápido de pruebas, define TLK_QUICKJOIN en Scripting Define Symbols.
#if TLK_QUICKJOIN
    public static bool QuickJoinEnabled = true;
#else
    public static bool QuickJoinEnabled = false;
#endif

    static GameObject _playerPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootEarly()
    {
        // Guard: si no hay NetworkManager en escena, creamos uno mínimo.
        if (Object.FindFirstObjectByType<NetworkManager>() == null)
        {
            var go = new GameObject("NetworkManager_BOOT");
            Object.DontDestroyOnLoad(go);
            var nm  = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            utp.SetConnectionData("127.0.0.1", 7777);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
    #if TLK_QUICKJOIN
        // Si NO quieres quick-join, quita el define TLK_QUICKJOIN y todo este bloque no compila.
        if (!QuickJoinEnabled) return;

        // UI de pruebas local
        if (Object.FindFirstObjectByType<QuickJoinLocalUI>() == null)
        {
            var ui = new GameObject("MP_HARNESS_UI");
            ui.AddComponent<QuickJoinLocalUI>();
            Object.DontDestroyOnLoad(ui);
        }

        // En quick-join, apagamos la MainCamera que no es de red para evitar doble cámara
        var main = Camera.main;
        if (main != null && main.transform.GetComponentInParent<NetworkObject>() == null)
            main.gameObject.SetActive(false);

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // PlayerPrefab SOLO para quick-join (no tocamos la config del juego normal)
        _playerPrefab = Resources.Load<GameObject>("PlayerNetwork");
        if (_playerPrefab != null)
        {
            if (nm.NetworkConfig.PlayerPrefab == null)
                nm.NetworkConfig.PlayerPrefab = _playerPrefab;
            try { nm.AddNetworkPrefab(_playerPrefab); } catch { /* ya estaba */ }
        }

        // ConnectionApproval: en quick-join sí auto-creamos el player
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = OnApproval;

        // Hooks server para garantizar spawn en quick-join
        nm.OnServerStarted += () =>
        {
            if (nm.IsHost) nm.StartCoroutine(EnsurePlayerCo(nm.LocalClientId));
        };
        nm.OnClientConnectedCallback += clientId =>
        {
            if (!nm.IsServer) return;
            nm.StartCoroutine(EnsurePlayerCo(clientId));
        };
    #else
        // Sin TLK_QUICKJOIN no hacemos nada en Boot()
        return;
    #endif
    }


    // SOLO usado cuando QuickJoinEnabled == true
    static void OnApproval(NetworkManager.ConnectionApprovalRequest req,
                           NetworkManager.ConnectionApprovalResponse resp)
    {
        resp.Approved = true;
        resp.CreatePlayerObject = true;

        // Hash del prefab (compatibilidad varias versiones NGO)
        uint hash = 0;
        var pp = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        var no = pp != null ? pp.GetComponent<NetworkObject>() : null;
        if (no != null)
        {
            var t = no.GetType();
            var pGlobal = t.GetProperty("GlobalObjectIdHash", BindingFlags.Public | BindingFlags.Instance);
            var pPrefab = t.GetProperty("PrefabIdHash",       BindingFlags.Public | BindingFlags.Instance);
            var p = pGlobal ?? pPrefab;
            if (p != null)
            {
                var val = p.GetValue(no);
                if      (val is uint  u ) hash = u;
                else if (val is int   i ) hash = unchecked((uint)i);
                else if (val is ulong ul) hash = unchecked((uint)ul);
                else if (val is long  l ) hash = unchecked((uint)l);
            }
        }
        resp.PlayerPrefabHash = hash;
        resp.Position = Vector3.zero;
        resp.Rotation = Quaternion.identity;
    }

    // SOLO quick-join
    static IEnumerator EnsurePlayerCo(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        yield return null; yield return null; // deja que NGO haga su spawn automático

        var existing = nm.SpawnManager.GetPlayerNetworkObject(clientId);
        if (existing == null)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[Harness] Prefab PlayerNetwork no encontrado para quick-join.");
                yield break;
            }
            var go = Object.Instantiate(_playerPrefab);
            var no = go.GetComponent<NetworkObject>();
            if (no == null) { Debug.LogError("[Harness] Prefab sin NetworkObject"); yield break; }
            no.SpawnAsPlayerObject(clientId);
            existing = no;
        }

        if (existing.OwnerClientId != clientId)
            existing.ChangeOwnership(clientId);
    }
}
