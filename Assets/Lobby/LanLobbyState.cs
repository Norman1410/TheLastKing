using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct LanPlayerEntry : INetworkSerializable, System.IEquatable<LanPlayerEntry>
{
    public ulong ClientId;
    public FixedString64Bytes Name;
    public bool Ready;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref ClientId);
        s.SerializeValue(ref Name);
        s.SerializeValue(ref Ready);
    }

    public bool Equals(LanPlayerEntry other) => ClientId == other.ClientId;
}

public class LanLobbyState : NetworkBehaviour
{
    public static LanLobbyState Instance;

    [Header("Misma escena: deja el nombre actual")]
    [SerializeField] string gameplaySceneName = "SampleScene";

    public NetworkList<LanPlayerEntry> Players;
    public readonly NetworkVariable<bool> GameStarted =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        Players = new NetworkList<LanPlayerEntry>();
        Instance = this;
        DontDestroyOnLoad(gameObject); // por si luego cambias de escena
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

            bool exists = false;
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].ClientId == NetworkManager.LocalClientId) { exists = true; break; }

            if (!exists)
            {
                Players.Add(new LanPlayerEntry {
                    ClientId = NetworkManager.LocalClientId,
                    Name = PlayerName.Get(),
                    Ready = false
                });
            }
        }

        if (IsClient)
            RegisterSelfServerRpc(PlayerName.Get());
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (GameStarted.Value)
            SpawnPlayerIfMissing(clientId); // late-joiner
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == clientId) { Players.RemoveAt(i); break; }
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterSelfServerRpc(string displayName, ServerRpcParams rpc = default)
    {
        var cid = rpc.Receive.SenderClientId;

        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == cid)
            { var e = Players[i]; e.Name = displayName; Players[i] = e; return; }

        Players.Add(new LanPlayerEntry { ClientId = cid, Name = displayName, Ready = false });
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReadyServerRpc(bool value, ServerRpcParams rpc = default)
    {
        var cid = rpc.Receive.SenderClientId;
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ClientId == cid)
            { var e = Players[i]; e.Ready = value; Players[i] = e; break; }
    }

    public bool AllReady()
    {
        if (Players.Count == 0) return false;
        for (int i = 0; i < Players.Count; i++)
            if (!Players[i].Ready) return false;
        return true;
    }

    // Host pulsa "Iniciar"
    public void StartMatchAsHost()
    {
        if (!IsServer || !AllReady()) return;

        GameStarted.Value = true;

        var current = SceneManager.GetActiveScene().name;
        if (string.Equals(current, gameplaySceneName))
        {
            SpawnAllPlayersNow(); // misma escena
        }
        else
        {
            NetworkManager.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }

    void OnLoadEventCompleted(string sceneName,
                              LoadSceneMode mode,
                              List<ulong> clientsCompleted,
                              List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        if (!GameStarted.Value) return;
        if (!string.Equals(sceneName, gameplaySceneName)) return;

        SpawnAllPlayersNow();
    }

    void SpawnAllPlayersNow()
    {
        var prefab = NetworkManager.NetworkConfig.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[LAN] PlayerPrefab no asignado en NetworkManager.");
            return;
        }

        foreach (var clientId in NetworkManager.ConnectedClientsIds)
            SpawnPlayerIfMissing(clientId);
    }

    void SpawnPlayerIfMissing(ulong clientId)
    {
        var prefab = NetworkManager.NetworkConfig.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[LAN] PlayerPrefab nulo.");
            return;
        }

        // ¿ya tenía PlayerObject?
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc) &&
            cc.PlayerObject != null && cc.PlayerObject.IsSpawned)
        {
            Debug.Log($"[LAN] Cliente {clientId} ya tenía PlayerObject.");
            return;
        }

        // Elegir punto de spawn (usa hijos de NetworkSpawnPoints)
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        var spRoot = FindObjectOfType<NetworkSpawnPoints>();
        if (spRoot != null)
        {
            var t = spRoot.transform;
            int total = t.childCount;
            if (total > 0)
            {
                // índice estable: por defecto clientId % total
                int idx = (int)(clientId % (ulong)total);

                // Si tienes lista de Players (NetworkList<LanPlayerEntry>), intenta
                // asignar por orden en esa lista para que host=0 tome el primer punto:
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].ClientId == clientId) { idx = i % total; break; }
                }

                var p = t.GetChild(idx);
                pos = p.position;
                rot = p.rotation;
            }
        }

        // Instanciar ya en la pose elegida
        var go = Instantiate(prefab, pos, rot);
        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[LAN] El PlayerPrefab no tiene NetworkObject.");
            Destroy(go);
            return;
        }

        // ¡Clave! Lo convertimos en el PlayerObject de ese cliente (host incluido)
        no.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        Debug.Log($"[LAN] SpawnAsPlayerObject -> client {clientId} en {pos}.");
    }

}