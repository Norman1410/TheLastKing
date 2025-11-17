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

    // Track clients that have been eliminated in this match (they remain connected but won't play)
    private System.Collections.Generic.HashSet<ulong> eliminatedClients = new System.Collections.Generic.HashSet<ulong>();
    // Last round duration in seconds (used when scheduling next rounds)
    // Default changed to 60s (1 minute) per request
    private int lastRoundDurationSeconds = 60;

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

            // Start periodic spawn monitor for debugging
            StartCoroutine(MonitorSpawnStatus());

            bool exists = false;
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].ClientId == NetworkManager.LocalClientId) { exists = true; break; }

            if (!exists)
            {
                Players.Add(new LanPlayerEntry
                {
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
        // If the client was eliminated earlier in the match, ignore spawn requests
        if (eliminatedClients != null && eliminatedClients.Contains(cid))
        {
            Debug.Log($"[LanLobbyState] ClientReadyForSpawnServerRpc: client {cid} is eliminated; ignoring spawn request.");
            return;
        }

        SpawnPlayerIfMissing(cid);
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"[LanLobbyState] OnClientConnected: clientId={clientId}");

        // Corrutina que intentará spawnear al jugador cuando el juego haya empezado
        StartCoroutine(EnsureSpawnForClient(clientId));

        // Ya NO marcamos como eliminado aquí.
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
            {
                var e = Players[i];
                e.Ready = value;
                Players[i] = e;
                break;
            }

        Debug.Log($"[LanLobbyState] ToggleReadyServerRpc -> client {cid} set Ready={value}");
        LogPlayerStates();
    }


    public bool AllReady()
    {
        // Si no hay jugadores, claramente no se puede empezar
        if (Players.Count == 0)
            return false;

        // TODOS los jugadores (host y clientes) deben estar listos
        for (int i = 0; i < Players.Count; i++)
        {
            if (!Players[i].Ready)
                return false;
        }

        return true;
    }


    // Host pulsa "Iniciar"
    public void StartMatchAsHost()
    {
        bool allReady = AllReady();
        Debug.Log($"[LanLobbyState] StartMatchAsHost called. IsServer={IsServer}, AllReady={allReady}");

        if (!IsServer)
        {
            Debug.LogWarning("[LanLobbyState] Cannot start match: Not server");
            return;
        }

        // 🔴 AQUÍ CAMBIAMOS LA LÓGICA
        if (!allReady)
        {
            // Intentamos detectar si estamos en modo Relay para NO bloquear
            bool isRelay = false;
            try
            {
                // NetRuntime.Mode es el mismo enum que usas en LobbyController
                isRelay = (NetRuntime.Mode == NetMode.Relay);
            }
            catch { }

            if (!isRelay)
            {
                // LAN normal: seguimos exigiendo que todos estén listos
                Debug.LogWarning("[LanLobbyState] Cannot start match (LAN): Not all players are ready");
                LogPlayerStates();
                return;
            }
            else
            {
                // RELAY: confiamos en que UGS ya validó READY y continuamos
                Debug.LogWarning("[LanLobbyState] Not all players marked Ready en NetworkList, " +
                                 "pero estamos en Relay y UGS ya validó READY. Continuando de todas formas.");
            }
        }

        Debug.Log("[LanLobbyState] Starting match...");
        GameStarted.Value = true;

        var current = SceneManager.GetActiveScene().name;
        Debug.Log($"[LanLobbyState] Current scene: {current}, Target scene: {gameplaySceneName}");

        if (string.Equals(current, gameplaySceneName))
        {
            Debug.Log("[LanLobbyState] Already in gameplay scene, spawning players now");
            SpawnAllPlayersNow(); // misma escena
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
                // Start persistent spawn attempts until the player is spawned
                StartCoroutine(EnsureSpawnForClient(cid));
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
        // Wait until all connected clients have PlayerObjects spawned (or timeout)
        const int spawnChecks = 20; // checks
        const float spawnDelay = 0.5f; // seconds between checks (total ~10s)
        bool allSpawned = false;
        for (int check = 0; check < spawnChecks; check++)
        {
            allSpawned = true;
            try
            {
                foreach (var cid in NetworkManager.ConnectedClientsIds)
                {
                    if (NetworkManager.ConnectedClients.TryGetValue(cid, out var cc))
                    {
                        if (cc.PlayerObject == null || !cc.PlayerObject.IsSpawned)
                        {
                            allSpawned = false;
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LanLobbyState] Exception while checking spawn status: {ex}");
                allSpawned = false;
            }

            if (allSpawned) break;
            Debug.Log($"[LanLobbyState] Waiting for all PlayerObjects to be spawned... attempt {check + 1}/{spawnChecks}");
            yield return new WaitForSeconds(spawnDelay);
        }

        if (!allSpawned)
        {
            Debug.LogWarning("[LanLobbyState] Not all PlayerObjects were spawned before timer start timeout. Proceeding anyway.");
        }

        // Determine timer duration (default 60s)
        int seconds = 60;
        var ts = UnityEngine.Object.FindAnyObjectByType<TheLastKing.TimerStarter>();
        if (ts != null) seconds = ts.roundDuration;
        else
        {
            var ct = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (ct != null) seconds = ct.durationSeconds;
        }
        // store for next-round scheduling
        lastRoundDurationSeconds = seconds;

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

        // Start a server-side authoritative timer that will handle round end (despawn non-crowned players)
        try
        {
            if (IsServer)
            {
                Debug.Log($"[LanLobbyState] Starting server-side round timer for {seconds}s");
                StartCoroutine(RunServerRoundTimer(seconds));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LanLobbyState] Failed to start server-side round timer: " + e);
        }
    }

    System.Collections.IEnumerator RunServerRoundTimer(int seconds)
    {
        if (!IsServer) yield break;
        yield return new WaitForSeconds(seconds);
        if (!IsServer) yield break;
        Debug.Log("[LanLobbyState] Server round timer finished. Handling round end.");
        HandleRoundEnd();
    }

    // Public wrapper so other systems (TimerNetworkMessaging) can ask the lobby to start the server-side timer
    public void StartServerTimerPublic(int seconds)
    {
        if (!IsServer) return;
        Debug.Log($"[LanLobbyState] StartServerTimerPublic called for {seconds}s");
        StartCoroutine(RunServerRoundTimer(seconds));
    }

    void HandleRoundEnd()
    {
        if (!IsServer) return;

        var nm = NetworkManager;
        if (nm == null)
        {
            Debug.LogWarning("[LanLobbyState] NetworkManager null in HandleRoundEnd");
            return;
        }

        List<ulong> winners = new List<ulong>();
        List<PlayerRob> winnerPlayers = new List<PlayerRob>();

        // Iterate connected clients and check their PlayerObject for crown ownership
        foreach (var clientId in nm.ConnectedClientsIds)
        {
            if (!nm.ConnectedClients.TryGetValue(clientId, out var cc)) continue;

            var po = cc.PlayerObject;
            bool hasCrown = false;
            if (po != null && po.IsSpawned)
            {
                var pr = po.GetComponent<PlayerRob>();
                if (pr != null) hasCrown = pr.HasCrown();
            }

            if (hasCrown)
            {
                winners.Add(clientId);
                if (po != null)
                {
                    var pr = po.GetComponent<PlayerRob>();
                    if (pr != null) winnerPlayers.Add(pr);
                }
            }
        }

        // Despawn non-winners' PlayerObjects but keep them connected and mark them eliminated
        foreach (var clientId in nm.ConnectedClientsIds)
        {
            if (winners.Contains(clientId)) continue;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var cc)) continue;
            var po = cc.PlayerObject;
            if (po != null && po.IsSpawned)
            {
                try
                {
                    Debug.Log($"[LanLobbyState] Notifying and despawning PlayerObject for client {clientId} (lost round)");

                    // Notify the client that they are eliminated (will run only on that client)
                    try
                    {
                        var clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
                        NotifyEliminatedClientRpc(clientRpcParams);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[LanLobbyState] Failed to send elimination RPC to client {clientId}: {ex}");
                    }

                    // Despawn and mark eliminated so they won't be included in next rounds
                    po.Despawn(destroy: true);
                    eliminatedClients.Add(clientId);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LanLobbyState] Failed to despawn PlayerObject for client {clientId}: {e}");
                }
            }
        }

        // Update network variables so clients know the timer stopped
        TimerActive.Value = false;
        TimerDuration.Value = 0;

        // Broadcast winners to all clients so UI can respond
        try
        {
            AnnounceWinnersClientRpc(winners.ToArray());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LanLobbyState] AnnounceWinnersClientRpc failed: " + e);
        }
        Debug.Log($"[LanLobbyState] Round end processed. Winners count: {winners.Count}");

        // If only one winner left -> game over. Otherwise start next round among winners.
        if (winners.Count <= 1)
        {
            ulong winnerId = winners.Count == 1 ? winners[0] : NetworkManager.ServerClientId;
            Debug.Log($"[LanLobbyState] Match finished. Winner: {winnerId}");
            try
            {
                AnnounceMatchWinnerClientRpc(winnerId);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[LanLobbyState] AnnounceMatchWinnerClientRpc failed: " + ex);
            }

            // End match state
            GameStarted.Value = false;
            TimerActive.Value = false;
            TimerDuration.Value = 0;
            return;
        }

        // Otherwise schedule next round using only the winnerPlayers
        StartCoroutine(StartNextRoundCoroutine(winnerPlayers));
    }

    System.Collections.IEnumerator StartNextRoundCoroutine(List<PlayerRob> players)
    {
        // Short intermission
        yield return new WaitForSeconds(3f);

        if (!IsServer) yield break;

        var cgm = UnityEngine.Object.FindAnyObjectByType<CrownGameManager>();
        if (cgm == null)
        {
            Debug.LogWarning("[LanLobbyState] CrownGameManager not found for next-round assignment.");
            yield break;
        }

        // Assign crowns among the survivors
        cgm.AssignCrownsToPlayers(players);

        // Set timer network vars and start server-side timer
        TimerDuration.Value = lastRoundDurationSeconds;
        TimerActive.Value = true;

        // Broadcast start to clients (use reflection for TimerNetworkMessaging.BroadcastStart)
        try
        {
            var tType = System.Type.GetType("TheLastKing.TimerNetworkMessaging, Assembly-CSharp");
            if (tType != null)
            {
                var mi = tType.GetMethod("BroadcastStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null)
                {
                    mi.Invoke(null, new object[] { lastRoundDurationSeconds });
                    Debug.Log($"[LanLobbyState] Next round BroadcastStart invoked for {lastRoundDurationSeconds}s");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LanLobbyState] Next round BroadcastStart failed: " + e);
        }

        // Start server timer
        StartCoroutine(RunServerRoundTimer(lastRoundDurationSeconds));
    }

    [ClientRpc]
    void AnnounceMatchWinnerClientRpc(ulong winnerClientId, ClientRpcParams rpcParams = default)
    {
        // Client-side: resolve winner name if possible and show UI
        string winnerName = winnerClientId.ToString();
        try
        {
            var lan = LanLobbyState.Instance;
            if (lan != null)
            {
                for (int i = 0; i < lan.Players.Count; i++)
                {
                    if (lan.Players[i].ClientId == winnerClientId)
                    {
                        winnerName = lan.Players[i].Name.ToString();
                        break;
                    }
                }
            }
        }
        catch { }

        bool isLocal = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == winnerClientId;
        Debug.Log($"[LanLobbyState] Match winner is {winnerName} (id={winnerClientId}). isLocal={isLocal}");

        // Show runtime UI
        try
        {
            // WinnerUI is in global namespace
            var t = System.Type.GetType("WinnerUI, Assembly-CSharp");
            if (t != null)
            {
                var mi = t.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null) mi.Invoke(null, new object[] { winnerName, isLocal });
                else Debug.LogWarning("[LanLobbyState] WinnerUI.Show method not found via reflection");
            }
            else
            {
                Debug.LogWarning("[LanLobbyState] WinnerUI type not found via reflection; UI will not be shown.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LanLobbyState] Could not display WinnerUI: " + e);
        }
    }

    // Notify a specific client that they have been eliminated and should enter spectator mode
    [ClientRpc]
    void NotifyEliminatedClientRpc(ClientRpcParams rpcParams = default)
    {
        // This runs on the client that was targeted
        Debug.Log("[LanLobbyState] You have been eliminated and are now a spectator.");

        // Try to disable local input/components if any player object exists
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                var po = nm.LocalClient.PlayerObject;
                var pr = po.GetComponent<PlayerRob>();
                if (pr != null)
                {
                    // disable input components if present
                    var pi = po.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                    if (pi != null) pi.enabled = false;

                    var pm = po.GetComponent<PlayerMovement>();
                    if (pm != null) pm.enabled = false;

                    // Hide local HUD elements that should not be visible to eliminated players
                    try
                    {
                        // Hide crown HUD (keeps timer alone)
                        var crownHud = UnityEngine.Object.FindAnyObjectByType<CrownHud>();
                        if (crownHud != null)
                        {
                            crownHud.gameObject.SetActive(false);
                            Debug.Log("[LanLobbyState] CrownHud hidden for eliminated client.");
                        }

                        // Hide powers HUD
                        var powersHud = UnityEngine.Object.FindAnyObjectByType<PowersHUD>();
                        if (powersHud != null)
                        {
                            powersHud.gameObject.SetActive(false);
                            Debug.Log("[LanLobbyState] PowersHUD hidden for eliminated client.");
                        }
                    }
                    catch (System.Exception exHud)
                    {
                        Debug.LogWarning("[LanLobbyState] Failed to hide HUDs for eliminated client: " + exHud);
                    }
                }
                // Destroy local player object if present (server will despawn it too)
                // But avoid double-destroy; the server will call despawn.
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LanLobbyState] NotifyEliminatedClientRpc handling failed: {e}");
        }
    }

    [ClientRpc]
    void AnnounceWinnersClientRpc(ulong[] winnerClientIds, ClientRpcParams rpcParams = default)
    {
        // Client-side: simple log; UI can subscribe to LanLobbyState.Instance to show winners
        string s = "(none)";
        if (winnerClientIds != null && winnerClientIds.Length > 0)
        {
            s = string.Join(",", winnerClientIds);
        }
        Debug.Log($"[LanLobbyState] Winners announced: {s}");
    }

    System.Collections.IEnumerator RetrySpawnMissingPlayers()
    {
        // Increased retry window: try longer to ensure clients spawn in slow networks
        const int maxAttempts = 60; // ~30 seconds with delay 0.5s
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
                        // Also start a persistent ensure coroutine for this client
                        StartCoroutine(EnsureSpawnForClient(clientId));
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

    System.Collections.IEnumerator EnsureSpawnForClient(ulong clientId)
    {
        if (!IsServer) yield break;

        const int maxAttempts = 80; // total ~40 seconds
        const float delay = 0.5f;

        // If the game hasn't started yet, wait until it starts before attempting to spawn
        if (!GameStarted.Value)
        {
            Debug.Log($"[LanLobbyState] EnsureSpawnForClient({clientId}) waiting for GameStarted...");
            yield return new WaitUntil(() => GameStarted.Value);
            Debug.Log($"[LanLobbyState] EnsureSpawnForClient({clientId}) detected GameStarted, proceeding to spawn attempts.");
        }

        for (int i = 0; i < maxAttempts; i++)
        {
            // If client disconnected, stop (ConnectedClientsIds may not support Contains)
            bool foundId = false;
            foreach (var id in NetworkManager.ConnectedClientsIds) { if (id == clientId) { foundId = true; break; } }
            if (!foundId) yield break;

            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc))
            {
                if (cc.PlayerObject != null && cc.PlayerObject.IsSpawned)
                {
                    Debug.Log($"[LanLobbyState] EnsureSpawnForClient: client {clientId} already has PlayerObject.");
                    yield break;
                }
            }

            // If this client is eliminated (spectator), stop trying
            if (eliminatedClients != null && eliminatedClients.Contains(clientId))
            {
                Debug.Log($"[LanLobbyState] EnsureSpawnForClient: client {clientId} is eliminated; stopping spawn attempts.");
                yield break;
            }

            // Attempt spawn
            try
            {
                SpawnPlayerIfMissing(clientId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LanLobbyState] EnsureSpawnForClient spawn attempt failed for {clientId}: {e}");
            }

            // Wait and retry
            yield return new WaitForSeconds(delay);
        }

        Debug.LogWarning($"[LanLobbyState] EnsureSpawnForClient: failed to spawn client {clientId} after retries.");
    }

    System.Collections.IEnumerator MonitorSpawnStatus()
    {
        if (!IsServer) yield break;
        const float interval = 5f;
        while (true)
        {
            try
            {
                var nm = NetworkManager;
                if (nm == null)
                {
                    Debug.LogWarning("[LanLobbyState] MonitorSpawnStatus: NetworkManager null");
                }
                else
                {
                    string s = "[LanLobbyState] Spawn status:";
                    foreach (var cid in nm.ConnectedClientsIds)
                    {
                        bool hasPlayer = false;
                        if (nm.ConnectedClients.TryGetValue(cid, out var cc))
                        {
                            hasPlayer = cc.PlayerObject != null && cc.PlayerObject.IsSpawned;
                        }
                        s += $" \n  Client {cid}: PlayerObjectSpawned={hasPlayer}";
                    }
                    Debug.Log(s);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LanLobbyState] MonitorSpawnStatus exception: {e}");
            }

            yield return new WaitForSeconds(interval);
        }
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
        // Do not spawn clients that have been eliminated (they remain as spectators)
        if (eliminatedClients != null && eliminatedClients.Contains(clientId))
        {
            Debug.Log($"[LAN] Client {clientId} is eliminated; skipping spawn.");
            return;
        }

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

        // Robust placement: disable physics/controller while we position, try multiple nearby places if occupied,
        // then re-enable physics next frame.
        var ccComp = no.GetComponent<CharacterController>();
        var rb = no.GetComponent<Rigidbody>();

        try
        {
            // Temporarily disable CharacterController and make Rigidbody kinematic to avoid immediate physics pushes
            if (ccComp != null && ccComp.enabled) ccComp.enabled = false;
            if (rb != null)
            {
                rb.isKinematic = true;
                // zero velocity using reflection to avoid obsolete API differences across Unity versions
                try
                {
                    var prop = rb.GetType().GetProperty("velocity");
                    if (prop != null && prop.CanWrite)
                        prop.SetValue(rb, Vector3.zero, null);
                    else
                    {
                        var prop2 = rb.GetType().GetProperty("linearVelocity");
                        if (prop2 != null && prop2.CanWrite)
                            prop2.SetValue(rb, Vector3.zero, null);
                    }
                }
                catch { }
            }

            // Try several offset positions around desired spawn to avoid overlaps that cause falling through
            Vector3 chosenPos = pos;
            Quaternion chosenRot = rot;
            bool placed = false;
            const int maxAttempts = 12;
            const float step = 1.0f;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // spiral offsets: center, right, forward, left, back, then expanded radius
                int ring = attempt / 4;
                int idx = attempt % 4;
                Vector3 offset = Vector3.zero;
                switch (idx)
                {
                    case 0: offset = Vector3.zero; break;
                    case 1: offset = Vector3.right * (step * (1 + ring)); break;
                    case 2: offset = Vector3.forward * (step * (1 + ring)); break;
                    case 3: offset = Vector3.left * (step * (1 + ring)); break;
                }

                Vector3 tryPos = pos + offset;

                // Raycast down to find ground height at that XY
                RaycastHit hit;
                if (Physics.Raycast(tryPos + Vector3.up * 10f, Vector3.down, out hit, 50f))
                {
                    tryPos.y = hit.point.y + 0.5f; // place slightly above ground
                }
                else
                {
                    // if no ground found, place slightly above original y
                    tryPos.y = pos.y + 1.0f;
                }

                // Check capsule overlap at tryPos to ensure not spawning inside geometry or another player
                float radius = 0.35f;
                float height = 1.8f;
                Vector3 p1 = tryPos + Vector3.up * (radius);
                Vector3 p2 = tryPos + Vector3.up * (height - radius);
                Collider[] cols = Physics.OverlapCapsule(p1, p2, radius);
                bool overlap = false;
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    if (c.attachedRigidbody != null)
                    {
                        // allow self
                        if (c.attachedRigidbody.gameObject == no.gameObject) continue;
                    }
                    if (c.isTrigger) continue;
                    overlap = true; break;
                }

                if (!overlap)
                {
                    chosenPos = tryPos;
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // fallback: move up a bit to avoid being stuck in floor
                chosenPos = pos + Vector3.up * 2.0f;
                Debug.LogWarning($"[LAN] Couldn't find non-overlapping spawn near {pos}. Using fallback {chosenPos}");
            }

            // Apply final position & rotation
            no.transform.position = chosenPos;
            no.transform.rotation = chosenRot;

            // Ensure rigidbody zeroed and re-enabled next frame
            StartCoroutine(FinishSpawnEnable(no, ccComp, rb));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LAN] Exception while placing PlayerObject for client {clientId}: {ex}");
            // Best effort: set transform and enable components
            try { no.transform.position = pos; no.transform.rotation = rot; } catch { }
            if (rb != null) rb.isKinematic = false;
            if (ccComp != null) ccComp.enabled = true;
        }

        Debug.Log($"[LAN] SpawnAsPlayerObject -> client {clientId} at {no.transform.position} (requested {pos}).");

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

    System.Collections.IEnumerator FinishSpawnEnable(NetworkObject no, CharacterController ccComp, Rigidbody rb)
    {
        // Wait a frame so physics and Netcode can settle, then re-enable movement components
        yield return null;
        try
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                // zero velocity using reflection to avoid obsolete API differences
                try
                {
                    var prop = rb.GetType().GetProperty("velocity");
                    if (prop != null && prop.CanWrite)
                        prop.SetValue(rb, Vector3.zero, null);
                    else
                    {
                        var prop2 = rb.GetType().GetProperty("linearVelocity");
                        if (prop2 != null && prop2.CanWrite)
                            prop2.SetValue(rb, Vector3.zero, null);
                    }
                }
                catch { }
            }

            if (ccComp != null)
            {
                ccComp.enabled = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LAN] FinishSpawnEnable exception: {e}");
        }

        // Wait an extra frame to be safe
        yield return null;
    }

}