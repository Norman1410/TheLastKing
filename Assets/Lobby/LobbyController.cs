using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// UGS
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class LobbyController : MonoBehaviour
{
    NetMode Mode => NetRuntime.Mode; // tu enum/manejador existente

    [Header("LAN")]
    public string lanIp = "127.0.0.1";
    public ushort lanPort = 7777;

    [Header("Relay + Lobby")]
    public int maxPlayers = 8;
    public string playerDisplayName = "Jugador";

    Lobby _lobby;
    string _status = "";
    string _joinLobbyCodeInput = "";
    bool _isHost = false;
    bool _isReady = false;
    // NUEVO: flag para esconder el HUD en Relay
    bool _hideHudRelay = false;


    float _pollEvery = 1.5f;
    Coroutine _pollCo;

    async void Awake()
    {
        if (Mode == NetMode.Relay)
            await EnsureServices();
    }

    void OnDestroy()
    {
        if (_pollCo != null) StopCoroutine(_pollCo);
    }

    async Task EnsureServices()
    {
        // Retry a few times because in built players network/services may take longer
        const int maxAttempts = 3;
        int attempt = 0;
        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log("[Lobby] Initializing Unity Services (attempt " + attempt + ")...");
                    await UnityServices.InitializeAsync();
                    Debug.Log("[Lobby] Unity Services initialized.");
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[Lobby] Signing in anonymously (attempt " + attempt + ")...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log("[Lobby] Signed in. PlayerId=" + AuthenticationService.Instance.PlayerId);
                }

                // success
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Lobby] EnsureServices attempt " + attempt + " failed: " + e);
                _status = "Error inicializando servicios (intento " + attempt + ")";
                // small backoff
                await Task.Delay(1000 * attempt);
            }
        }

        throw new Exception("No se pudieron inicializar los Unity Services tras varios intentos.");
    }
    
    void OnGUI()
    {
        // Ocultar HUD en LAN cuando el juego ya inició
        if (Mode == NetMode.LAN)
        {
            var st = LanLobbyState.Instance;
            if (st != null && st.GameStarted.Value)
                return;
        }

        // Ocultar HUD en RELAY cuando ya estamos empezando/conectados
        if (Mode == NetMode.Relay)
        {
            if (_hideHudRelay
                || GetLobbyData("state") == "starting"
                || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
                return;
        }
        GUI.color = Color.black; // texto en negro

        if (Mode == NetMode.LAN)
        {
            DrawLanUI();
        }
        else if (Mode == NetMode.Relay)
        {
            DrawRelayUI();
        }
    }


    // ==================== LAN con LOBBY ====================

// ==================== LAN con LOBBY ====================

    void EnsureLanStateSpawned()
    {
        // En “misma escena”, LanLobbyState ya está puesto como GameObject en la escena.
        // Aquí solo verificamos y avisamos si se olvidaron de colocarlo.
        if (LanLobbyState.Instance != null) return;

        Debug.LogWarning("[LAN] Falta el GameObject 'LanLobbyState' en la escena. " +
                        "Añádelo con NetworkObject + LanLobbyState y deja gameplaySceneName = \"SampleScene\".");
    }

    // Connection Approval: aprobar pero NO crear PlayerObject en el lobby
    void InstallLobbyConnectionApproval(NetworkManager nm)
    {
        try
        {
            if (nm == null)
            {
                Debug.LogError("[Lobby] InstallLobbyConnectionApproval called with null NetworkManager");
                return;
            }

            if (nm.NetworkConfig == null)
            {
                Debug.LogError("[Lobby] NetworkManager.NetworkConfig is null. Cannot install connection approval.");
                return;
            }

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = (req, resp) =>
            {
                resp.Approved = true;
                resp.CreatePlayerObject = false; // <- clave para lobby en misma escena
                resp.Pending = false;
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Lobby] InstallLobbyConnectionApproval exception: {e}\nNetworkManager={nm}\nNetworkConfig={ (nm!=null? nm.NetworkConfig.ToString() : "<null>") }");
        }
    }


    void DrawLanUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            // Try to create one on demand
            try {
                var nsType = System.Type.GetType("NetSetupOnce");
                if (nsType != null)
                {
                    var mi = nsType.GetMethod("EnsureNetworkManagerPublic", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (mi != null) mi.Invoke(null, null);
                }
            } catch { }

            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                GUILayout.Label("NetworkManager no encontrado en escena.");
                return;
            }
        }

    // Si aún no hay red activa, mostramos crear/unirse
    if (!nm.IsListening)
        {
            // HOST: crear lobby LAN (no entra al mapa)
        // HOST: crear lobby LAN (no entra al mapa)
            if (GUILayout.Button("Crear Lobby LAN (Host)"))
            {
                // Aprobación: en LAN NO auto-spawneamos player en el lobby
                try {
                    // Defensive re-init: ensure NetworkConfig exists and transport is assigned before starting
                    if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();

                    UnityTransport utp = null;
                    // prefer existing transport
                    try { if (nm.NetworkConfig.NetworkTransport is UnityTransport utpCast) utp = utpCast; } catch { }
                    if (utp == null) utp = nm.gameObject.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();

                    // assign transport into NetworkConfig
                    if (nm.NetworkConfig.NetworkTransport == null) nm.NetworkConfig.NetworkTransport = utp;

                    // ensure PlayerPrefab
                    if (nm.NetworkConfig.PlayerPrefab == null)
                    {
                        var playerPrefabCandidate = Resources.Load<GameObject>("PlayerNetwork");
                        if (playerPrefabCandidate != null) nm.NetworkConfig.PlayerPrefab = playerPrefabCandidate;
                    }

                    InstallLobbyConnectionApproval(nm);

                    // If NetworkManager thinks it's listening or in a weird state, shutdown first
                    if (nm.IsListening || nm.IsServer || nm.IsClient)
                    {
                        try { nm.Shutdown(); } catch { }
                    }

                    // ensure transport connection data
                    try { utp.SetConnectionData("0.0.0.0", lanPort, "0.0.0.0"); } catch { }

                    // Host LAN
                    if (!nm.StartHost())
                    {
                        _status = "No se pudo iniciar Host LAN.";
                        Debug.LogError("StartHost failed - NetworkManager state: IsListening=" + nm.IsListening + ", IsServer=" + nm.IsServer + ", IsClient=" + nm.IsClient);
                        return;
                    }
                } catch (System.Exception e) { Debug.LogError("Error creating host: " + e); _status = "No se pudo iniciar Host LAN (excepción)."; return; }

                _isHost = true;

                // En “misma escena” solo verificamos que el GO LanLobbyState exista
                EnsureLanStateSpawned();

                _status = $"Lobby LAN creado. Conéctense a {GetLocalIp()}:{lanPort}";
            }


            // CLIENTE: unirse a lobby LAN
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(30));
            lanIp = GUILayout.TextField(lanIp, GUILayout.Width(160));
            GUILayout.Label("Puerto:", GUILayout.Width(56));
            lanPort = ushort.TryParse(GUILayout.TextField(lanPort.ToString(), GUILayout.Width(70)), out var p) ? p : (ushort)7777;
            if (GUILayout.Button("Unirse LAN"))
            {
                try
                {
                    if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
                    InstallLobbyConnectionApproval(nm);

                    UnityTransport utpClient = null;
                    if (nm.NetworkConfig != null && nm.NetworkConfig.NetworkTransport is UnityTransport utpCast2)
                        utpClient = utpCast2;
                    if (utpClient == null)
                        utpClient = nm.gameObject.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
                    if (nm.NetworkConfig != null && nm.NetworkConfig.NetworkTransport == null)
                        nm.NetworkConfig.NetworkTransport = utpClient;
                    utpClient.SetConnectionData(lanIp, lanPort);
                    nm.StartClient();
                }
                catch (System.Exception e) { Debug.LogError("Error joining LAN: " + e); }

                _isHost = false;
                _status = $"Conectando a {lanIp}:{lanPort}…";
            }
            GUILayout.EndHorizontal();

            // Nombre visible
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nombre:", GUILayout.Width(60));
            playerDisplayName = GUILayout.TextField(playerDisplayName, GUILayout.Width(200));
            if (GUILayout.Button("Guardar nombre"))
            {
                PlayerName.Set(string.IsNullOrWhiteSpace(playerDisplayName) ? "Jugador" : playerDisplayName.Trim());
                _status = $"Nombre guardado: {PlayerName.Get()}";
            }
            GUILayout.EndHorizontal();

            return;
        }

        // --- Ya conectados: UI del lobby LAN ---
        var state = LanLobbyState.Instance;

        GUILayout.Label(_isHost
            ? $"Lobby LAN (Host): {GetLocalIp()}:{lanPort}"
            : $"Lobby LAN (Cliente) conectado a {lanIp}:{lanPort}");

        GUILayout.Space(4);
        GUILayout.Label("Jugadores:");
        if (state != null)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                var p = state.Players[i];
                GUILayout.Label($" - {p.Name}  (Ready: {p.Ready})");
            }
        }
        else
        {
            GUILayout.Label("Sincronizando lobby...");
        }

        GUILayout.Space(8);
        if (GUILayout.Button(_isReady ? "Quitar 'Listo'" : "Marcar 'Listo'"))
        {
            _isReady = !_isReady;
            state?.ToggleReadyServerRpc(_isReady);
        }

        if (_isHost && state != null)
        {
            GUI.enabled = state.AllReady();
            if (GUILayout.Button("Iniciar Juego (Host)"))
            {
                state.StartMatchAsHost();
                _status = "Iniciando juego…";
            }
            GUI.enabled = true;
        }

        if (GUILayout.Button("Salir del Lobby"))
        {
            nm.Shutdown();
            _isHost = false;
            _isReady = false;
            _status = "Desconectado.";
        }
    }

    // ==================== RELAY (igual a tu flujo) ====================

    void DrawRelayUI()
    {
        if (_lobby == null)
        {
            if (GUILayout.Button("Crear Lobby (Internet)"))
                _ = CreateLobby();

            GUILayout.BeginHorizontal();
            _joinLobbyCodeInput = GUILayout.TextField(_joinLobbyCodeInput, GUILayout.Width(160));
            if (GUILayout.Button("Unirse por Código (Lobby)"))
                _ = JoinLobbyByCode(_joinLobbyCodeInput);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nombre:", GUILayout.Width(60));
            playerDisplayName = GUILayout.TextField(playerDisplayName, GUILayout.Width(200));
            if (GUILayout.Button("Guardar nombre"))
            {
                PlayerName.Set(string.IsNullOrWhiteSpace(playerDisplayName) ? "Jugador" : playerDisplayName.Trim());
                _status = $"Nombre guardado: {PlayerName.Get()}";
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Space(4);
            GUILayout.Label("Jugadores:");
            foreach (var p in _lobby.Players)
            {
                string n = p.Data != null && p.Data.ContainsKey("name") ? p.Data["name"].Value : p.Id.Substring(0, Math.Min(6, p.Id.Length));
                string r = p.Data != null && p.Data.ContainsKey("ready") ? p.Data["ready"].Value : "false";
                GUILayout.Label($" - {n}  (Ready: {r})");
            }

            GUILayout.Space(8);
            if (GUILayout.Button(_isReady ? "Quitar 'Listo'" : "Marcar 'Listo'"))
                _ = ToggleReady(!_isReady);

            if (_isHost)
            {
                if (GUILayout.Button("Iniciar Juego (Host)"))
                    StartGame();
            }

            if (GUILayout.Button("Salir del Lobby"))
                _ = LeaveLobby();

            if (!string.IsNullOrEmpty(_lobby.LobbyCode))
            {
                GUILayout.Space(6);
                GUILayout.Label($"LobbyCode: {_lobby.LobbyCode}");
                if (GUILayout.Button("Copiar LobbyCode"))
                    GUIUtility.systemCopyBuffer = _lobby.LobbyCode;
            }
        }
    }

    // -------- Relay + Lobby --------

    async Task CreateLobby()
    {
        try
        {
            await EnsureServices();
            _isHost = true;

            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "name",  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, PlayerName.GetOr(playerDisplayName)) },
                { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") }
            };

            var opts = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player(AuthenticationService.Instance.PlayerId, null, playerData),
                Data = new Dictionary<string, DataObject>
                {
                    { "state",    new DataObject(DataObject.VisibilityOptions.Public, "lobby") },
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, "") }
                }
            };

            _lobby = await Lobbies.Instance.CreateLobbyAsync("Sala-TLK", Mathf.Clamp(maxPlayers, 2, 16), opts);
            _status = $"Lobby creado: {_lobby.LobbyCode}";
            StartPolling();
        }
        catch (Exception e)
        {
            _status = $"CreateLobby error: {e.Message}";
            Debug.LogError(e);
        }
    }

    async Task JoinLobbyByCode(string code)
    {
        try
        {
            await EnsureServices();
            _isHost = false;

            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "name",  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, PlayerName.GetOr(playerDisplayName)) },
                { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") }
            };

            var opts = new JoinLobbyByCodeOptions
            {
                Player = new Player(AuthenticationService.Instance.PlayerId, null, playerData)
            };

            _lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code, opts);
            _status = $"Unido al lobby: {code}";
            StartPolling();
        }
        catch (Exception e)
        {
            _status = $"JoinLobby error: {e.Message}";
            Debug.LogError(e);
        }
    }

    async Task ToggleReady(bool value)
    {
        try
        {
            _isReady = value;
            await Lobbies.Instance.UpdatePlayerAsync(
                _lobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "name",  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, PlayerName.GetOr(playerDisplayName)) },
                        { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, value ? "true" : "false") }
                    }
                });
            _status = value ? "Marcado 'Listo'." : "Quitado 'Listo'.";
        }
        catch (Exception e)
        {
            _status = $"Ready error: {e.Message}";
            Debug.LogError(e);
        }
    }

    async Task LeaveLobby()
    {
        try
        {
            if (_lobby == null) return;
            if (_isHost)
                await Lobbies.Instance.DeleteLobbyAsync(_lobby.Id);
            else
                await Lobbies.Instance.RemovePlayerAsync(_lobby.Id, AuthenticationService.Instance.PlayerId);

            _lobby = null;
            _isHost = false;
            _isReady = false;
            if (_pollCo != null) StopCoroutine(_pollCo);
            _status = "Saliste del lobby.";
        }
        catch (Exception e)
        {
            _status = $"Leave error: {e.Message}";
            Debug.LogError(e);
        }
    }

    void StartPolling()
    {
        if (_pollCo != null) StopCoroutine(_pollCo);
        _pollCo = StartCoroutine(PollLobby());
    }

    IEnumerator PollLobby()
    {
        while (_lobby != null)
        {
            yield return new WaitForSeconds(_pollEvery);

            try
            {
                _ = RefreshLobby();

                // Clientes: si el host puso "starting", conectarse por Relay
                if (!_isHost && Mode == NetMode.Relay &&
                    GetLobbyData("state") == "starting" &&
                    !NetworkManager.Singleton.IsListening)
                {
                    string code = GetLobbyData("joinCode");
                    if (!string.IsNullOrEmpty(code))
                        _ = StartRelayClient(code);
                }
            }
            catch (Exception e)
            {
                _status = $"Poll error: {e.Message}";
            }
        }
    }

    async Task RefreshLobby()
    {
        if (_lobby == null) return;
        _lobby = await Lobbies.Instance.GetLobbyAsync(_lobby.Id);
    }

    // ===== Inicio de juego según modo =====

    public async void StartGame()
    {
        await EnsureServices();
        if (Mode == NetMode.LAN)
        {
            _status = "Usa el botón 'Iniciar Juego (Host)' en el Lobby LAN.";
            return;
        }

        // Relay + Lobby
        if (!_isHost) { _status = "Solo el host puede iniciar."; return; }
        if (!AllReady(_lobby)) { _status = "No todos están 'Listo'."; return; }

        try
        {
            await EnsureServices();
            Allocation alloc;
            try
            {
                Debug.Log("[Lobby] Creating Relay allocation...");
                alloc = await RelayService.Instance.CreateAllocationAsync(_lobby.MaxPlayers - 1);
                Debug.Log("[Lobby] Allocation created. ID=" + alloc.AllocationId);
            }
            catch (Exception e)
            {
                Debug.LogError("[Lobby] Relay CreateAllocationAsync failed: " + e);
                _status = "Relay allocation error: " + e.Message;
                return;
            }

            string joinCode;
            try
            {
                Debug.Log("[Lobby] Requesting join code for allocation " + alloc.AllocationId + "...");
                joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                Debug.Log("[Lobby] Join code obtained: " + joinCode);
            }
            catch (Exception e)
            {
                Debug.LogError("[Lobby] Relay GetJoinCodeAsync failed: " + e);
                _status = "Relay join code error: " + e.Message;
                return;
            }

            await Lobbies.Instance.UpdateLobbyAsync(_lobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "state",    new DataObject(DataObject.VisibilityOptions.Public, "starting") },
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            });

            await StartRelayHost(alloc);
            _status = $"Juego iniciado. RelayCode: {joinCode}";
            _hideHudRelay = true;
            if (_pollCo != null) StopCoroutine(_pollCo); // opcional
        }
        catch (Exception e)
        {
            _status = $"StartGame error: {e.Message}";
            Debug.LogError(e);
        }
    }

    // ===== Relay helpers =====

    async Task StartRelayHost(Allocation alloc)
    {
        await EnsureServices();
        // Defensive checks: ensure NetworkManager and transport exist
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[Relay] StartRelayHost: NetworkManager.Singleton is null");
            _status = "No NetworkManager disponible.";
            return;
        }

        if (nm.NetworkConfig == null)
        {
            Debug.LogWarning("[Relay] StartRelayHost: NetworkConfig null, creating one.");
            nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
        }

        UnityTransport transport = null;
        try { transport = nm.NetworkConfig.NetworkTransport as UnityTransport; } catch { }
        if (transport == null)
        {
            transport = nm.gameObject.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
            if (nm.NetworkConfig.NetworkTransport == null)
                nm.NetworkConfig.NetworkTransport = transport;
        }

        if (transport == null)
        {
            Debug.LogError("[Relay] StartRelayHost: No UnityTransport available");
            _status = "No se pudo iniciar Host (Relay): falta transport.";
            return;
        }

        var data = new RelayServerData(alloc, "dtls");
        try
        {
            Debug.Log("[Relay] Applying RelayServerData to transport...");
            transport.SetRelayServerData(data);
            Debug.Log("[Relay] RelayServerData applied.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] SetRelayServerData failed: " + e);
            _status = "Error al configurar Relay.";
            return;
        }

        // give one frame for transport to apply settings
        await Task.Yield();

        try
        {
            Debug.Log("[Relay] Starting Host...");
            if (!nm.StartHost())
            {
                _status = "No se pudo iniciar Host (Relay).";
                Debug.LogError("[Relay] StartHost failed - NetworkManager state: IsListening=" + nm.IsListening + ", IsServer=" + nm.IsServer + ", IsClient=" + nm.IsClient);
                return;
            }
            Debug.Log("[Relay] Host started successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] StartHost threw exception: " + e);
            _status = "StartHost exception: " + e.Message;
            return;
        }

        _isHost = true;
        _status = "Lobby Relay creado.";
        _hideHudRelay = true;
    }


    async Task StartRelayClient(string joinCode)
    {
        await EnsureServices();
        // Try JoinAllocation with retries because network in built players may be flaky
        const int joinAttempts = 3;
        for (int a = 1; a <= joinAttempts; a++)
        {
            try
            {
                Debug.Log($"[Relay] Joining allocation with code {joinCode} (attempt {a})...");
                var join = await RelayService.Instance.JoinAllocationAsync(joinCode);
                Debug.Log("[Relay] JoinAllocation succeeded.");

                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    Debug.LogError("[Relay] StartRelayClient: NetworkManager.Singleton is null");
                    _status = "No NetworkManager disponible.";
                    return;
                }

                if (nm.NetworkConfig == null)
                {
                    Debug.LogWarning("[Relay] StartRelayClient: NetworkConfig null, creating one.");
                    nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
                }

                UnityTransport transport = null;
                try { transport = nm.NetworkConfig.NetworkTransport as UnityTransport; } catch { }
                if (transport == null)
                {
                    transport = nm.gameObject.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
                    if (nm.NetworkConfig.NetworkTransport == null)
                        nm.NetworkConfig.NetworkTransport = transport;
                }

                if (transport == null)
                {
                    Debug.LogError("[Relay] StartRelayClient: No UnityTransport available");
                    _status = "No se pudo conectar por Relay: falta transport.";
                    return;
                }

                var data = new RelayServerData(join, "dtls");
                try
                {
                    Debug.Log("[Relay] Applying RelayServerData to transport (client)...");
                    transport.SetRelayServerData(data);
                    Debug.Log("[Relay] RelayServerData applied on client transport.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[Relay] SetRelayServerData failed (client): " + e);
                    _status = "Error al configurar Relay.";
                    return;
                }

                await Task.Yield();

                try
                {
                    Debug.Log("[Relay] Starting client...");
                    nm.StartClient();
                    // give a moment to settle
                    await Task.Delay(200);
                    if (nm.IsClient)
                    {
                        _status = "Cliente conectado por Relay.";
                        _hideHudRelay = true;
                        if (_pollCo != null) StopCoroutine(_pollCo); // opcional
                        Debug.Log("[Relay] Client is connected (IsClient=true).");
                    }
                    else
                    {
                        Debug.LogWarning("[Relay] StartClient invoked but NetworkManager reports IsClient=" + nm.IsClient + ", IsListening=" + nm.IsListening);
                        _status = "StartClient iniciado, pero no se detectó conexión inmediatamente.";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[Relay] StartClient threw exception: " + e);
                    _status = "StartClient exception: " + e.Message;
                }

                // success or failure handled - exit retry loop
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Relay] JoinAllocation attempt {a} failed: {e}");
                _status = $"Error uniendo Relay (intento {a}): {e.Message}";
                await Task.Delay(500 * a);
            }
        }

        Debug.LogError("[Relay] All JoinAllocation attempts failed. Aborting client start.");
        return;
    }

    // ===== Utils =====

    string GetLobbyData(string key)
    {
        if (_lobby == null || _lobby.Data == null || !_lobby.Data.ContainsKey(key)) return "";
        return _lobby.Data[key].Value;
    }

    bool AllReady(Lobby l)
    {
        if (l == null) return false;
        foreach (var p in l.Players)
            if (p.Data == null || !p.Data.ContainsKey("ready") || p.Data["ready"].Value != "true")
                return false;
        return true;
    }

    string GetLocalIp()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}
