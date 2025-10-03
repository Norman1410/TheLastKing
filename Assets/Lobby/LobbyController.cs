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
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
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
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = (req, resp) =>
        {
            resp.Approved = true;
            resp.CreatePlayerObject = false; // <- clave para lobby en misma escena
            resp.Pending = false;
        };
    }


    void DrawLanUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            GUILayout.Label("NetworkManager no encontrado en escena.");
            return;
        }

        // Si aún no hay red activa, mostramos crear/unirse
        if (!nm.IsListening)
        {
            // HOST: crear lobby LAN (no entra al mapa)
        // HOST: crear lobby LAN (no entra al mapa)
            if (GUILayout.Button("Crear Lobby LAN (Host)"))
            {
                // Aprobación: en LAN NO auto-spawneamos player en el lobby
                InstallLobbyConnectionApproval(nm);

                // Escuchar en todas las interfaces
                var utp = (UnityTransport)nm.NetworkConfig.NetworkTransport;
                utp.SetConnectionData("0.0.0.0", lanPort, "0.0.0.0");

                // Host LAN
                if (!nm.StartHost())
                {
                    _status = "No se pudo iniciar Host LAN.";
                    return;
                }

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
                InstallLobbyConnectionApproval(nm);

                var utp = (UnityTransport)nm.NetworkConfig.NetworkTransport;
                utp.SetConnectionData(lanIp, lanPort);
                nm.StartClient();

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
            var alloc = await RelayService.Instance.CreateAllocationAsync(_lobby.MaxPlayers - 1);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

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
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        var data = new RelayServerData(alloc, "dtls");
        transport.SetRelayServerData(data);

        await Task.Yield();

        if (!NetworkManager.Singleton.StartHost())
        {
            _status = "No se pudo iniciar Host (Relay).";
            return;
        }

        _isHost = true;
        _status = "Lobby Relay creado.";
        _hideHudRelay = true;

    }


    async Task StartRelayClient(string joinCode)
    {
        var join = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        var data = new RelayServerData(join, "dtls");
        transport.SetRelayServerData(data);
        await Task.Yield();
        NetworkManager.Singleton.StartClient();
        _status = "Cliente conectado por Relay.";
        _hideHudRelay = true;
        if (_pollCo != null) StopCoroutine(_pollCo); // opcional
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
