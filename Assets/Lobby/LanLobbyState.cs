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

    [Header("Gameplay Scene Name (leave current scene name to spawn in same scene)")]
    [SerializeField] string gameplaySceneName = "Game";

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
        {
            RegisterSelfServerRpc(PlayerName.Get());
            // subscribe to local scene load so client can notify server when it finished loading gameplay scene
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLocalSceneLoaded;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
        if (IsClient)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLocalSceneLoaded;
        }
    }

    bool _reportedReadyForSpawn = false;

    void OnLocalSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Only run on clients
        if (!IsClient) return;

        if (!string.Equals(scene.name, gameplaySceneName)) return;

        // If the game hasn't been started by host, ignore
        if (!GameStarted.Value) return;

        if (_reportedReadyForSpawn) return;

        _reportedReadyForSpawn = true;
        Debug.Log($"[LanLobbyState] Client local scene loaded ('{scene.name}'). Reporting ready to server.");
        ClientReadyForSpawnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void ClientReadyForSpawnServerRpc(ServerRpcParams rpc = default)
    {
        if (!IsServer) return;
        var cid = rpc.Receive.SenderClientId;
        Debug.Log($"[LanLobbyState] Received ClientReadyForSpawnServerRpc from {cid}. Spawning if missing.");
        SpawnPlayerIfMissing(cid);
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
        
        // Host doesn't need to be ready, only clients
        for (int i = 0; i < Players.Count; i++)
        {
            // Skip the host (server's client ID)
            if (Players[i].ClientId == NetworkManager.ServerClientId)
                continue;
                
            if (!Players[i].Ready) 
                return false;
        }
        
        return true;
    }

    // Host pulsa "Iniciar"
    public void StartMatchAsHost()
    {
        Debug.Log($"[LanLobbyState] StartMatchAsHost called. IsServer={IsServer}, AllReady={AllReady()}");
        
        if (!IsServer)
        {
            Debug.LogWarning("[LanLobbyState] Cannot start match: Not server");
            return;
        }
        
        if (!AllReady())
        {
            Debug.LogWarning("[LanLobbyState] Cannot start match: Not all players are ready");
            LogPlayerStates();
            return;
        }

        Debug.Log("[LanLobbyState] Starting match...");
        GameStarted.Value = true;

        var current = SceneManager.GetActiveScene().name;
        Debug.Log($"[LanLobbyState] Current scene: {current}, Target scene: {gameplaySceneName}");
        
        if (string.Equals(current, gameplaySceneName))
        {
            Debug.Log("[LanLobbyState] Already in gameplay scene, spawning players now");
            SpawnAllPlayersNow(); // misma escena
        }
        else
        {
            Debug.Log($"[LanLobbyState] Loading scene: {gameplaySceneName}");
            NetworkManager.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }
    
    void LogPlayerStates()
    {
        Debug.Log($"[LanLobbyState] Player count: {Players.Count}");
        for (int i = 0; i < Players.Count; i++)
        {
            Debug.Log($"  Player {i}: ClientId={Players[i].ClientId}, Name={Players[i].Name}, Ready={Players[i].Ready}");
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

        Debug.Log($"[LanLobbyState] OnLoadEventCompleted for scene '{sceneName}'. Completed: {clientsCompleted.Count}, TimedOut: {clientsTimedOut.Count}");
        if (clientsCompleted != null && clientsCompleted.Count > 0)
        {
            foreach (var cid in clientsCompleted)
            {
                Debug.Log($"[LanLobbyState] Spawning player for completed client {cid}");
                SpawnPlayerIfMissing(cid);
            }
        }

        if (clientsTimedOut != null && clientsTimedOut.Count > 0)
        {
            foreach (var cid in clientsTimedOut)
            {
                Debug.LogWarning($"[LanLobbyState] Client {cid} timed out while loading scene. Will retry spawn later when they finish loading.");
            }
        }

        // Start a short retry loop to cover clients that finish loading a bit later
        StartCoroutine(RetrySpawnMissingPlayers());
    }

    System.Collections.IEnumerator RetrySpawnMissingPlayers()
    {
        const int maxAttempts = 20; // ~10 seconds with delay 0.5s
        const float delay = 0.5f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool allSpawned = true;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc))
                {
                    if (cc.PlayerObject == null || !cc.PlayerObject.IsSpawned)
                    {
                        // Try to spawn; SpawnPlayerIfMissing contains its own checks and logs
                        SpawnPlayerIfMissing(clientId);
                        // If we attempted spawn, assume not all spawned yet
                        allSpawned = false;
                    }
                }
            }

            if (allSpawned) yield break;
            yield return new WaitForSeconds(delay);
        }

        Debug.LogWarning("[LanLobbyState] RetrySpawnMissingPlayers finished: some clients may not have PlayerObjects spawned.");
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

    var spRoot = UnityEngine.Object.FindAnyObjectByType<NetworkSpawnPoints>();
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