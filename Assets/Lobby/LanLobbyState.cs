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
    
    // Timer sync: when host starts timer, set this so late-joining clients can start their timer too
    public readonly NetworkVariable<int> TimerDuration =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> TimerActive =
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
            
            // Subscribe to timer state changes so late-joining clients can start their timer
            TimerActive.OnValueChanged += OnTimerActiveChanged;
            
            // If timer is already active when we spawn, start it immediately
            if (TimerActive.Value && TimerDuration.Value > 0)
            {
                Debug.Log($"[LanLobbyState] Client spawned with timer already active ({TimerDuration.Value}s). Starting client timer...");
                StartClientTimer(TimerDuration.Value);
            }
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
            TimerActive.OnValueChanged -= OnTimerActiveChanged;
        }
    }

    void OnTimerActiveChanged(bool wasActive, bool isActive)
    {
        if (!IsClient || IsServer) return; // only run on pure clients
        
        if (isActive && TimerDuration.Value > 0)
        {
            Debug.Log($"[LanLobbyState] Client detected timer activated remotely ({TimerDuration.Value}s). Starting client timer...");
            StartClientTimer(TimerDuration.Value);
        }
    }
    
    void StartClientTimer(int seconds)
    {
        var ct = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
        
        // If no UI exists, create one at runtime
        if (ct == null)
        {
            Debug.Log("[LanLobbyState] No CountdownTimerUI found on client, creating runtime UI...");
            try
            {
                ct = CountdownTimerUI.CreateRuntimeTimerUI();
                Debug.Log("[LanLobbyState] Runtime CountdownTimerUI created for client.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[LanLobbyState] Failed to create runtime UI: " + ex);
            }
        }
        
        if (ct != null)
        {
            try
            {
                ct.EnsureAndStart(seconds);
                Debug.Log($"[LanLobbyState] Client timer UI started with sprites for {seconds}s");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LanLobbyState] ct.EnsureAndStart failed: " + e);
                ct.gameObject.SetActive(true);
                ct.StartTimer(seconds);
            }
        }
        else
        {
            Debug.LogWarning("[LanLobbyState] Could not start client timer - CountdownTimerUI creation failed");
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
        Debug.Log($"[LanLobbyState] RegisterSelfServerRpc from {cid} with name '{displayName}'");

        // Update existing entry if present
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == cid)
            {
                var e = Players[i]; e.Name = displayName; Players[i] = e;
                var applied = UpdatePlayerObjectNetworkName(cid, displayName);
                Debug.Log($"[LanLobbyState] Updated Players entry for {cid}. Applied to PlayerObject={applied}");
                return;
            }
        }

        // Add new entry
        Players.Add(new LanPlayerEntry { ClientId = cid, Name = displayName, Ready = false });

        // Try to update playerobject now; if missing, start a retry coroutine
        var appliedNow = UpdatePlayerObjectNetworkName(cid, displayName);
        Debug.Log($"[LanLobbyState] Added Players entry for {cid}. Applied to PlayerObject now={appliedNow}");
        if (!appliedNow)
        {
            // start retry coroutine on server
            StartCoroutine(RetryApplyNameToPlayerObject(cid, displayName));
        }

        // Debug: print current Players list
        Debug.Log($"[LanLobbyState] Players list after RegisterSelf: count={Players.Count}");
        for (int i = 0; i < Players.Count; i++)
            Debug.Log($"  Player[{i}] ClientId={Players[i].ClientId}, Name={Players[i].Name}, Ready={Players[i].Ready}");
    }

    // Returns true if applied, false if no playerobject found
    bool UpdatePlayerObjectNetworkName(ulong clientId, string displayName)
    {
    // If the player's PlayerObject is spawned on the server, set its PlayerNetworkName.DisplayName
    if (!IsServer) return false;
    if (NetworkManager == null) return false;
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc))
        {
            var po = cc.PlayerObject;
            if (po != null && po.IsSpawned)
            {
                var pnn = po.GetComponent<PlayerNetworkDisplayName>();
                if (pnn != null)
                {
                    pnn.DisplayName.Value = new Unity.Collections.FixedString64Bytes(displayName ?? "Jugador");
                    Debug.Log($"[LanLobbyState] Applied DisplayName='{displayName}' to PlayerObject for client {clientId}");
                    return true;
                }
                else
                {
                    Debug.Log($"[LanLobbyState] PlayerObject for client {clientId} has no PlayerNetworkDisplayName component");
                }
            }
            else
            {
                Debug.Log($"[LanLobbyState] PlayerObject for client {clientId} is null or not spawned (po={po})");
            }
        }
        else
        {
            Debug.Log($"[LanLobbyState] No connected client entry for clientId {clientId}");
        }

        return false;
    }

    System.Collections.IEnumerator RetryApplyNameToPlayerObject(ulong clientId, string displayName)
    {
        const int attempts = 10;
        const float delay = 0.5f;
        for (int i = 0; i < attempts; i++)
        {
            if (UpdatePlayerObjectNetworkName(clientId, displayName)) yield break;
            yield return new WaitForSeconds(delay);
        }
        Debug.LogWarning($"[LanLobbyState] Could not apply displayName '{displayName}' to PlayerObject for client {clientId} after retries.");
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
            // Ensure timer starts on server even if we didn't change scenes
            StartCoroutine(StartTimerAfterSpawn());
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
        
        // After spawning players, start the timer on server and broadcast to clients
        StartCoroutine(StartTimerAfterSpawn());
    }
    
    System.Collections.IEnumerator StartTimerAfterSpawn()
    {
        // Wait a moment for spawn to complete
        yield return new WaitForSeconds(1.5f);
        
        if (!IsServer) yield break;
        
        // Determine timer duration
        int seconds = 180;
        var ts = UnityEngine.Object.FindAnyObjectByType<TheLastKing.TimerStarter>();
        if (ts != null) seconds = ts.roundDuration;
        else 
        { 
            var ct = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>(); 
            if (ct != null) seconds = ct.durationSeconds; 
        }
        
        Debug.Log($"[LanLobbyState] Starting timer for {seconds} seconds on server");
        
        // Update NetworkVariables so late-joining clients will see the timer state
        TimerDuration.Value = seconds;
        TimerActive.Value = true;
        
        // Start server's local timer using EnsureAndStart for sprite UI
        var ctHost = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
        if (ctHost != null)
        {
            try 
            { 
                ctHost.EnsureAndStart(seconds);
                Debug.Log("[LanLobbyState] Server timer UI started with sprites");
            }
            catch (System.Exception e) 
            { 
                Debug.LogWarning("[LanLobbyState] ctHost.EnsureAndStart failed: " + e);
                ctHost.gameObject.SetActive(true); 
                ctHost.StartTimer(seconds); 
            }
        }
        else if (ts != null)
        {
            ts.StartRound();
        }
        else
        {
                Debug.LogWarning("[LanLobbyState] No CountdownTimerUI or TimerStarter found on server - creating runtime TimerUI");
                try
                {
                    var created = CountdownTimerUI.CreateRuntimeTimerUI();
                    if (created != null)
                    {
                        created.EnsureAndStart(seconds);
                        Debug.Log("[LanLobbyState] Created runtime TimerUI and started timer on server.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[LanLobbyState] Failed to create runtime TimerUI: " + e);
                }
        }
        
        // Broadcast to clients via TimerNetworkMessaging (with detailed logs)
        try
        {
            var tType = System.Type.GetType("TheLastKing.TimerNetworkMessaging, Assembly-CSharp");
            if (tType != null)
            {
                // Use BroadcastStart which now logs each client individually
                var mi = tType.GetMethod("BroadcastStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null) 
                {
                    mi.Invoke(null, new object[] { seconds });
                    Debug.Log($"[LanLobbyState] BroadcastStart invoked for {seconds}s");
                }
                else
                {
                    Debug.LogWarning("[LanLobbyState] BroadcastStart method not found");
                }
            }
            else
            {
                Debug.LogWarning("[LanLobbyState] TimerNetworkMessaging type not found");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LanLobbyState] BroadcastStart failed: " + e);
        }
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
        if (spRoot != null && spRoot.Count > 0)
        {
            int total = spRoot.Count;
            // índice estable: por defecto clientId % total
            int idx = (int)(clientId % (ulong)total);

            // Si tienes lista de Players (NetworkList<LanPlayerEntry>), intenta
            // asignar por orden en esa lista para que host=0 tome el primer punto:
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == clientId) { idx = i % total; break; }
            }

            // Try to find a free spawn index nearby; prefer idx but pick the first free slot
            int chosen = -1;
            for (int offset = 0; offset < total; offset++)
            {
                int tryIdx = (idx + offset) % total;
                try
                {
                    if (spRoot.IsFree(tryIdx)) { chosen = tryIdx; break; }
                }
                catch { }
            }

            if (chosen == -1)
            {
                // none free; fallback to original idx
                chosen = idx;
                Debug.LogWarning($"[LAN] No free spawn points found. Using index {chosen} even if occupied.");
            }

            pos = spRoot.GetPoint(chosen);
            rot = spRoot.GetRotation(chosen);
            Debug.Log($"[LAN] Selected spawn point {chosen} for client {clientId} at {pos}");
        }
        else
        {
            // No spawn points defined in scene. Choose a safe fallback above ground near origin.
            Debug.LogWarning("[LAN] No NetworkSpawnPoints found in scene. Using fallback spawn position.");
            Vector3 fallback = new Vector3(0f, 3f, 0f);
            // Try raycast down from above origin to find ground
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(fallback.x, 50f, fallback.z), Vector3.down, out hit, 100f))
            {
                fallback.y = hit.point.y + 0.5f;
                Debug.Log($"[LAN] Fallback spawn adjusted to ground at {fallback}");
            }
            pos = fallback;
            rot = Quaternion.identity;
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

        // Ensure the spawned object is positioned and its collider/controller enabled
        try
        {
            no.transform.position = pos;
            no.transform.rotation = rot;
            var ccComp = no.GetComponent<CharacterController>();
            if (ccComp != null && !ccComp.enabled) ccComp.enabled = true;
            var rb = no.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }
        catch { }

        Debug.Log($"[LAN] SpawnAsPlayerObject -> client {clientId} en {pos}.");

        // If we have a Players entry for this client, apply its name to the spawned PlayerObject
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId)
            {
                var name = Players[i].Name.ToString();
                var pnn = no.GetComponent<PlayerNetworkDisplayName>();
                if (pnn != null)
                {
                    pnn.DisplayName.Value = new Unity.Collections.FixedString64Bytes(name ?? "Jugador");
                    Debug.Log($"[LAN] Applied name '{name}' to PlayerObject of client {clientId}.");
                }
                break;
            }
        }
    }

}