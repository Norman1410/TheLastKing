using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

// UGS
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

using TMPro;
using UnityEngine.UI;


public class LobbyController : MonoBehaviour
{
    NetMode Mode => NetRuntime.Mode; // tu enum/manejador existente

    [Header("LAN")]
    public string lanIp = "127.0.0.1";
    public ushort lanPort = 7777;

    [Header("Relay + Lobby")]
    public int maxPlayers = 8;
    public string playerDisplayName = "Jugador";
    [SerializeField] private string gameplaySceneName = "PruebasTheLastKing";

    [Header("LAN UI (Canvas)")]
    [SerializeField] TMP_InputField inputIpField;
    [SerializeField] TMP_InputField inputPortField;
    [SerializeField] TMP_InputField inputNameField;

    [SerializeField] Button createHostButton;
    [SerializeField] Button joinLanButton;
    [SerializeField] Button saveNameButton;

    [Header("Paneles UI")]
    [SerializeField] GameObject panelNetMode;      // Panel con botones LAN / ONLINE
    [SerializeField] GameObject panelLanLobby;     // Panel del Lobby LAN (IP/Nombre)
    [SerializeField] GameObject panelLanHUD;       // HUD moderno LAN
    [SerializeField] GameObject panelRelayLobby;   // Panel del Lobby ONLINE (crear/unirse)
    [SerializeField] GameObject panelRelayHUD;     // HUD moderno ONLINE (lista de jugadores)

    [Header("Relay Lobby (Canvas)")]
    [SerializeField] TMP_InputField relayCodeInputField;    // CodeInputField
    [SerializeField] TMP_InputField relayNameInputField;    // InputNameField
    [SerializeField] Button relayCreateLobbyButton;         // CreateRelayButton
    [SerializeField] Button relayJoinLobbyButton;           // JoinRelayButton
    [SerializeField] Button relaySaveNameButton;            // SaveNameButton
    [SerializeField] TMP_Text relayStatusText;              // (opcional) texto de estado

    [Header("Debug / Legacy HUD")]
    [SerializeField] bool useLegacyRelayHud = false;

    [Header("LAN HUD (Canvas)")]
    [SerializeField] TMP_Text lanHudStatusText;     // Texto de arriba (LobbyStatusText)
    [SerializeField] TMP_Text lanHudPlayersText;    // Texto de la lista (PlayersListText)
    [SerializeField] Button lanHudReadyButton;      // Botón "Marcar Listo"
    [SerializeField] Button lanHudStartGameButton;  // Botón "Iniciar Juego (Host)"
    [SerializeField] Button lanHudLeaveLobbyButton; // Botón "Salir del Lobby"

    [Header("Relay HUD (Canvas)")]
    [SerializeField] TMP_Text relayHudLobbyCodeText;   // Texto donde muestras el código (LobbyCODE)
    [SerializeField] TMP_Text relayHudPlayersText;     // Texto de la lista de jugadores
    [SerializeField] Button relayHudReadyButton;       // Botón "Marcar Listo"
    [SerializeField] Button relayHudStartGameButton;   // Botón "Iniciar Juego (Host)"
    [SerializeField] Button relayHudLeaveLobbyButton;  // Botón "Salir del Lobby"
    [SerializeField] Button relayHudCopyCodeButton;    // Botón "Copiar LobbyCode"

    [Header("Raíz UI NetMode (opcional)")]
    [SerializeField] GameObject netModeCanvasRoot; // aquí vamos a arrastrar NetModeCanvas

    Lobby _lobby;
    string _status = "";
    string _joinLobbyCodeInput = "";
    bool _isHost = false;
    bool _isReady = false;
    // NUEVO: flag para esconder el HUD en Relay
    bool _hideHudRelay = false;

    // Al principio de la clase LobbyController
    bool _lanSubscribedToList = false;


    float _pollEvery = 1.5f;
    Coroutine _pollCo;

    async void Awake()
    {
        // Inicializar campos de la UI LAN (si estamos en modo LAN)
        SetupLanUi();

        // Inicializar campos de la UI Relay (si estamos en modo Relay)
        SetupRelayUi();

        // Asegurar que el HUD nuevo LAN esté oculto al inicio
        if (panelLanHUD != null)
            panelLanHUD.SetActive(false);

        // Conectar botones del HUD LAN
        if (Mode == NetMode.LAN && panelLanHUD != null)
        {
            if (lanHudReadyButton != null)
                lanHudReadyButton.onClick.AddListener(OnLanHudToggleReady);

            if (lanHudStartGameButton != null)
                lanHudStartGameButton.onClick.AddListener(OnLanHudStartGame);

            if (lanHudLeaveLobbyButton != null)
                lanHudLeaveLobbyButton.onClick.AddListener(OnLanHudLeaveLobby);
        }

        // Conectar botones del Lobby ONLINE (Canvas)
        if (Mode == NetMode.Relay && panelRelayLobby != null)
        {
            if (relayCreateLobbyButton != null)
                relayCreateLobbyButton.onClick.AddListener(OnRelayUiCreateLobbyClicked);

            if (relayJoinLobbyButton != null)
                relayJoinLobbyButton.onClick.AddListener(OnRelayUiJoinLobbyClicked);

            if (relaySaveNameButton != null)
                relaySaveNameButton.onClick.AddListener(OnRelayUiSaveNameClicked);
        }

        // 🔹 NUEVO: Conectar botones del HUD ONLINE (la pantalla de tu captura)
        if (Mode == NetMode.Relay && panelRelayHUD != null)
        {
            if (relayHudReadyButton != null)
                relayHudReadyButton.onClick.AddListener(OnRelayHudToggleReady);

            if (relayHudStartGameButton != null)
                relayHudStartGameButton.onClick.AddListener(OnRelayHudStartGame);

            if (relayHudLeaveLobbyButton != null)
                relayHudLeaveLobbyButton.onClick.AddListener(OnRelayHudLeaveLobby);

            if (relayHudCopyCodeButton != null)
                relayHudCopyCodeButton.onClick.AddListener(OnRelayHudCopyCodeClicked);
        }

        // Inicializar servicios de UGS sólo en modo Relay
        if (Mode == NetMode.Relay)
            await EnsureServices();
    }


    void SetupLanUi()
    {
        if (Mode != NetMode.LAN)
            return;

        if (inputIpField != null)
            inputIpField.text = lanIp;

        if (inputPortField != null)
            inputPortField.text = lanPort.ToString();

        if (inputNameField != null)
            inputNameField.text = playerDisplayName;
    }

    void SetupRelayUi()
    {
        if (Mode != NetMode.Relay)
            return;

        // Asegurarnos de que el panel correcto esté activo
        if (panelNetMode != null) panelNetMode.SetActive(false);
        if (panelLanLobby != null) panelLanLobby.SetActive(false);
        if (panelLanHUD != null) panelLanHUD.SetActive(false);
        if (panelRelayLobby != null) panelRelayLobby.SetActive(true);
        if (panelRelayHUD != null) panelRelayHUD.SetActive(false); // 👈 HUD online oculto al inicio

        // Nombre inicial en el input
        if (relayNameInputField != null)
            relayNameInputField.text = playerDisplayName;

        // Limpiar el código al entrar
        if (relayCodeInputField != null)
            relayCodeInputField.text = "";

        // Mensaje de estado inicial
        if (relayStatusText != null)
            relayStatusText.text = "Lobby online listo para crear o unirse.";
    }


    public void OnLanHudToggleReady()
    {
        var state = LanLobbyState.Instance;
        if (state == null) return;

        _isReady = !_isReady;
        state.ToggleReadyServerRpc(_isReady);
        UpdateLanHudTexts();
    }

    public void OnLanHudStartGame()
    {
        var state = LanLobbyState.Instance;
        if (!_isHost || state == null) return;

        // 🔒 VERIFICACIÓN EXTRA DE SEGURIDAD EN CÓDIGO
        if (!state.AllReady())
        {
            _status = "No todos los jugadores están listos.";
            Debug.Log("[LAN] StartGame bloqueado: falta al menos un Ready=false");
            UpdateLanHudTexts();
            return;
        }

        state.StartMatchAsHost();
        _status = "Iniciando juego…";

        // Apagar HUD inmediatamente en el host
        if (panelLanHUD != null)
            panelLanHUD.SetActive(false);

        if (panelLanLobby != null)
            panelLanLobby.SetActive(false);

        // Apagar todo el Canvas de lobby si lo tenemos referenciado
        if (netModeCanvasRoot != null)
            netModeCanvasRoot.SetActive(false);

        UpdateLanHudTexts();

        // 🚀 NUEVO: cargar escena de juego para todos (host + clientes)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[LAN] Cargando escena de juego: " + gameplaySceneName);
            NetworkManager.Singleton.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );
        }
        else
        {
            Debug.LogWarning("[LAN] OnLanHudStartGame: NetworkManager no es server al intentar cargar escena.");
        }
    }



    // Suscribirse una sola vez al evento de la NetworkList de LanLobbyState
    void EnsureLanLobbySubscription()
    {
        if (_lanSubscribedToList)
            return;

        var state = LanLobbyState.Instance;
        if (state == null || state.Players == null)
            return;

        state.Players.OnListChanged += OnLanPlayersListChanged;
        _lanSubscribedToList = true;
        Debug.Log("[LobbyController] Subscribed to LanLobbyState.Players.OnListChanged");
    }

    // Desuscribirse por limpieza
    void UnsubscribeLanLobbySubscription()
    {
        if (!_lanSubscribedToList)
            return;

        var state = LanLobbyState.Instance;
        if (state != null && state.Players != null)
            state.Players.OnListChanged -= OnLanPlayersListChanged;

        _lanSubscribedToList = false;
        Debug.Log("[LobbyController] Unsubscribed from LanLobbyState.Players.OnListChanged");
    }

    // Callback cuando cambia la lista de jugadores en red
    void OnLanPlayersListChanged(Unity.Netcode.NetworkListEvent<LanPlayerEntry> e)
    {
        // Simplemente refrescamos el HUD
        UpdateLanHudTexts();
    }



    public void OnLanHudLeaveLobby()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
            nm.Shutdown();

        _isHost = false;
        _isReady = false;
        _status = "Desconectado.";

        if (panelLanHUD != null)
            panelLanHUD.SetActive(false);

        if (panelLanLobby != null)
            panelLanLobby.SetActive(true); // volver a la pantalla de IP/puerto
    }



    void OnDestroy()
    {
        if (_pollCo != null) StopCoroutine(_pollCo);
        UnsubscribeLanLobbySubscription();
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
        // HUD legacy de Relay completamente desactivado.
        // Esto evita que DrawRelayUI se ejecute y que use ToggleReady / botones viejos.
        return;
    }


    // ==================== LAN con LOBBY ====================
    // === Métodos llamados por la UI del Canvas (Lobby LAN) ===

    public void OnLanUiCreateHost()
    {
        // Leer puerto de la UI (si falla, usar 7777)
        if (inputPortField != null && ushort.TryParse(inputPortField.text, out var p))
            lanPort = p;
        else
            lanPort = 7777;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            // Intentar crear un NetworkManager si falta (igual que en DrawLanUI)
            try
            {
                var nsType = System.Type.GetType("NetSetupOnce");
                if (nsType != null)
                {
                    var mi = nsType.GetMethod("EnsureNetworkManagerPublic",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (mi != null) mi.Invoke(null, null);
                }
            }
            catch { }

            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                _status = "NetworkManager no encontrado en escena.";
                Debug.LogWarning(_status);
                return;
            }
        }

        try
        {
            // MISMO código que en DrawLanUI para crear el host
            if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();

            UnityTransport utp = null;
            try { if (nm.NetworkConfig.NetworkTransport is UnityTransport utpCast) utp = utpCast; } catch { }
            if (utp == null) utp = nm.gameObject.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();

            if (nm.NetworkConfig.NetworkTransport == null) nm.NetworkConfig.NetworkTransport = utp;

            if (nm.NetworkConfig.PlayerPrefab == null)
            {
                var playerPrefabCandidate = Resources.Load<GameObject>("PlayerNetwork");
                if (playerPrefabCandidate != null) nm.NetworkConfig.PlayerPrefab = playerPrefabCandidate;
            }

            InstallLobbyConnectionApproval(nm);

            if (nm.IsListening || nm.IsServer || nm.IsClient)
            {
                try { nm.Shutdown(); } catch { }
            }

            try { utp.SetConnectionData("0.0.0.0", lanPort, "0.0.0.0"); } catch { }

            if (!nm.StartHost())
            {
                _status = "No se pudo iniciar Host LAN.";
                Debug.LogError("StartHost failed - NetworkManager state: IsListening=" + nm.IsListening + ", IsServer=" + nm.IsServer + ", IsClient=" + nm.IsClient);
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error creating host: " + e);
            _status = "No se pudo iniciar Host LAN (excepción).";
            return;
        }

        _isHost = true;
        EnsureLanStateSpawned();
        _status = $"Lobby LAN creado. Conéctense a {GetLocalIp()}:{lanPort}";

        if (panelLanLobby != null)
            panelLanLobby.SetActive(false);

        if (panelLanHUD != null)
            panelLanHUD.SetActive(true);   // <- mostrar HUD moderno

        UpdateLanHudTexts();               // <- rellenar textos una vez

    }

    public void OnLanUiJoin()
    {
        // Leer IP
        if (inputIpField != null)
        {
            var txt = inputIpField.text;
            lanIp = string.IsNullOrWhiteSpace(txt) ? "127.0.0.1" : txt.Trim();
        }

        // Leer puerto
        if (inputPortField != null && ushort.TryParse(inputPortField.text, out var p))
            lanPort = p;
        else
            lanPort = 7777;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            try
            {
                var nsType = System.Type.GetType("NetSetupOnce");
                if (nsType != null)
                {
                    var mi = nsType.GetMethod("EnsureNetworkManagerPublic",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (mi != null) mi.Invoke(null, null);
                }
            }
            catch { }

            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                _status = "NetworkManager no encontrado en escena.";
                Debug.LogWarning(_status);
                return;
            }
        }

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
        catch (System.Exception e)
        {
            Debug.LogError("Error joining LAN: " + e);
        }

        _isHost = false;
        _status = $"Conectando a {lanIp}:{lanPort}…";

        if (panelLanLobby != null)
            panelLanLobby.SetActive(false);

        if (panelLanHUD != null)
            panelLanHUD.SetActive(true);   // <- mostrar HUD cuando el cliente se une

        UpdateLanHudTexts();

    }

    public void OnLanUiSaveName()
    {
        if (inputNameField != null)
            playerDisplayName = inputNameField.text;

        PlayerName.Set(string.IsNullOrWhiteSpace(playerDisplayName) ? "Jugador" : playerDisplayName.Trim());
        _status = $"Nombre guardado: {PlayerName.Get()}";
    }

    // === RELAY: métodos llamados por la UI del Canvas (Lobby ONLINE) ===

    public async void OnRelayUiCreateLobbyClicked()
    {
        if (relayStatusText != null)
            relayStatusText.text = "Creando lobby online...";

        await CreateLobby();   // Usa la lógica real que ya tienes

        if (_lobby != null)
        {
            // Soy host si mi PlayerId coincide con el HostId del lobby
            _isHost = (_lobby.HostId == AuthenticationService.Instance.PlayerId);
            _isReady = false;

            // Cambiamos de la pantalla de código al HUD moderno
            if (panelRelayLobby != null)
                panelRelayLobby.SetActive(false);

            if (panelRelayHUD != null)
                panelRelayHUD.SetActive(true);

            UpdateRelayHudTexts();

            if (relayStatusText != null)
                relayStatusText.text = "";
        }
        else
        {
            _isHost = false;
            _isReady = false;

            if (relayStatusText != null)
                relayStatusText.text = $"Error al crear lobby: {_status}";
        }
    }


    public async void OnRelayUiJoinLobbyClicked()
    {
        var code = relayCodeInputField != null ? relayCodeInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(code))
        {
            if (relayStatusText != null)
                relayStatusText.text = "Debe ingresar un código de lobby.";
            return;
        }

        if (relayStatusText != null)
            relayStatusText.text = $"Uniéndose al lobby {code}...";

        await JoinLobbyByCode(code);   // Usa tu método real

        if (_lobby != null)
        {
            // Verificamos si este jugador es el host o no
            _isHost = (_lobby.HostId == AuthenticationService.Instance.PlayerId);
            _isReady = false;

            if (panelRelayLobby != null)
                panelRelayLobby.SetActive(false);

            if (panelRelayHUD != null)
                panelRelayHUD.SetActive(true);

            UpdateRelayHudTexts();

            if (relayStatusText != null)
                relayStatusText.text = "";
        }
        else
        {
            _isHost = false;
            _isReady = false;

            if (relayStatusText != null)
                relayStatusText.text = $"Error al unirse: {_status}";
        }
    }


    public void OnRelayHudCopyCodeClicked()
    {
        // Si aún no hay lobby, no hay nada que copiar
        if (_lobby == null)
            return;

        // Copiar al portapapeles
        GUIUtility.systemCopyBuffer = _lobby.LobbyCode;

        Debug.Log($"[RelayUI] Lobby code copiado al portapapeles: {_lobby.LobbyCode}");

        // Opcional: feedback visual rápido
        if (relayHudLobbyCodeText != null)
            relayHudLobbyCodeText.text = $"LobbyCODE: {_lobby.LobbyCode} (copiado)";
    }

    public async void OnRelayHudToggleReady()
    {
        if (_lobby == null)
            return;

        try
        {
            // Alternamos el estado local
            _isReady = !_isReady;

            // Actualizar el texto del botón inmediatamente (feedback visual)
            if (relayHudReadyButton != null)
            {
                var label = relayHudReadyButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null)
                    label.text = _isReady ? "Quitar listo" : "Marcar listo";
            }

            // Opciones para actualizar SOLO el campo "ready" del jugador actual en el Lobby ONLINE (Relay)
            var options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    ["ready"] = new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Public,
                        _isReady ? "true" : "false"
                    )
                }
            };

            // 1) Actualizar READY en el Lobby de Relay (UGS)
            await Lobbies.Instance.UpdatePlayerAsync(
                _lobby.Id,
                AuthenticationService.Instance.PlayerId,
                options
            );

            // 2) Sincronizar READY también con LanLobbyState (NetworkList<LanPlayerEntry>)
            if (LanLobbyState.Instance != null)
            {
                try
                {
                    LanLobbyState.Instance.ToggleReadyServerRpc(_isReady);
                    Debug.Log($"[RelayUI] ToggleReadyServerRpc enviado a LanLobbyState con valor={_isReady}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[RelayUI] Error al llamar a ToggleReadyServerRpc: " + ex);
                }
            }
            else
            {
                Debug.LogWarning("[RelayUI] LanLobbyState.Instance es null, no se pudo sincronizar READY con LAN.");
            }

            // 3) Refrescamos el lobby para que la lista se actualice rápido
            await RefreshLobby();

            // 4) Actualizamos HUD online (lista de jugadores + botón iniciar)
            UpdateRelayHudTexts();

            Debug.Log($"[RelayUI] READY cambiado correctamente: {_isReady}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[RelayUI] Error al cambiar READY: " + e);
        }
    }



    public async void OnRelayHudLeaveLobby()
    {
        if (_lobby == null)
            return;

        try
        {
            // Si soy host, cierro el lobby completo.
            if (_isHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(_lobby.Id);
                Debug.Log("[RelayUI] Lobby eliminado por el host.");
            }
            else
            {
                // Si soy cliente, solo me salgo del lobby.
                await LobbyService.Instance.RemovePlayerAsync(
                    _lobby.Id,
                    AuthenticationService.Instance.PlayerId
                );
                Debug.Log("[RelayUI] Jugador salió del lobby.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[RelayUI] Error al salir del lobby: " + e);
        }
        finally
        {
            // Limpiar estado local
            _lobby = null;
            _isReady = false;

            // Volver a la pantalla de crear/unirse online
            if (panelRelayHUD != null)
                panelRelayHUD.SetActive(false);

            if (panelRelayLobby != null)
                panelRelayLobby.SetActive(true);

            if (relayStatusText != null)
                relayStatusText.text = "Saliste del lobby.";
        }
    }

    public void OnRelayHudStartGame()
    {
        // Solo el host puede iniciar
        if (!_isHost)
        {
            Debug.LogWarning("[RelayUI] Solo el host puede iniciar la partida.");
            return;
        }

        // Solo si todos están listos
        if (!AllRelayPlayersReady())
        {
            Debug.LogWarning("[RelayUI] No todos los jugadores están listos.");
            return;
        }

        // Reutilizamos la lógica general de StartGame()
        StartGame();
    }





    public void OnRelayUiSaveNameClicked()
    {
        if (relayNameInputField != null)
            playerDisplayName = relayNameInputField.text;

        PlayerName.Set(string.IsNullOrWhiteSpace(playerDisplayName)
            ? "Jugador"
            : playerDisplayName.Trim());

        if (relayStatusText != null)
            relayStatusText.text = $"Nombre guardado: {PlayerName.Get()}";

        //Debug.Log("[RelayUI] Nombre guardado para Relay: " + PlayerName.Get());
    }

    // === Botón "Volver" desde los lobbies (LAN u ONLINE) ===
    public void OnBackToModeSelect()
    {
        // Apagar paneles de lobby
        if (panelLanLobby != null)
            panelLanLobby.SetActive(false);

        if (panelRelayLobby != null)
            panelRelayLobby.SetActive(false);

        // También apagamos el HUD LAN por si venimos de ahí
        if (panelLanHUD != null)
            panelLanHUD.SetActive(false);

        // Y apagamos el HUD ONLINE por si venimos de Relay
        if (panelRelayHUD != null)
            panelRelayHUD.SetActive(false);

        // Volver a la pantalla de selección de modo
        if (panelNetMode != null)
            panelNetMode.SetActive(true);

        // Opcional: limpiar estado visual del Relay
        if (relayCodeInputField != null)
            relayCodeInputField.text = "";

        if (relayStatusText != null)
            relayStatusText.text = "";
    }

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
            Debug.LogError($"[Lobby] InstallLobbyConnectionApproval exception: {e}\nNetworkManager={nm}\nNetworkConfig={(nm != null ? nm.NetworkConfig.ToString() : "<null>")}");
        }
    }

    void UpdateLanHudTexts()
    {
        if (panelLanHUD == null || !panelLanHUD.activeSelf)
            return;

        // --- TEXTO DE ESTADO ARRIBA ---
        if (lanHudStatusText != null)
        {
            // Si ya estamos escuchando como host/cliente
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                lanHudStatusText.text = _isHost
                    ? $"Lobby LAN (Host): {GetLocalIp()}:{lanPort}"
                    : $"Lobby LAN (Cliente) conectado a {lanIp}:{lanPort}";
            }
            else
            {
                // Mientras no haya red, mostramos el último status
                lanHudStatusText.text = string.IsNullOrEmpty(_status)
                    ? "Inicializando lobby LAN..."
                    : _status;
            }
        }

        // --- LISTA DE JUGADORES ---
        if (lanHudPlayersText != null)
        {
            var state = LanLobbyState.Instance;

            // Si todavía no hay instancia de LanLobbyState
            if (state == null)
            {
                lanHudPlayersText.text =
                    "Jugadores conectados:\n(sincronizando lobby...)";
                return;
            }

            // Ya hay estado, veamos cuántos jugadores ve
            int count = state.Players.Count;

            var sb = new System.Text.StringBuilder();
            if (count == 0)
            {
                sb.AppendLine("(ninguno todavía)");
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var p = state.Players[i];
                    var readyText = p.Ready ? "Listo" : "No listo";
                    sb.AppendLine($"- {p.Name}  ({readyText})");
                }

            }

            lanHudPlayersText.text = sb.ToString();
        }

        // --- BOTÓN INICIAR JUEGO (HOST) ---
        if (lanHudStartGameButton != null)
        {
            var state = LanLobbyState.Instance;
            lanHudStartGameButton.interactable =
                _isHost && state != null && state.AllReady();
        }

        // --- BOTÓN LISTO / NO LISTO ---
        if (lanHudReadyButton != null)
        {
            var label = lanHudReadyButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null)
                label.text = _isReady ? "Quitar listo" : "Marcar listo";
        }
    }

    void UpdateRelayHudTexts()
    {
        // Log para depurar
        //Debug.Log(
        //    $"[RelayHUD] UpdateRelayHudTexts() - Mode={Mode}, " +
        //    $"lobby nulo={_lobby == null}, " +
        //    $"panelHUD activo={panelRelayHUD != null && panelRelayHUD.activeSelf}"
        //);

        // 🔴 ANTES:
        // if (Mode != NetMode.Relay)
        //     return;

        // ✅ AHORA: solo nos importa tener lobby y HUD activo
        if (_lobby == null)
            return;

        if (panelRelayHUD == null || !panelRelayHUD.activeSelf)
            return;

        // --- Código del lobby ---
        if (relayHudLobbyCodeText != null)
            relayHudLobbyCodeText.text = $"LobbyCODE: {_lobby.LobbyCode}";

        // --- Lista de jugadores ---
        if (relayHudPlayersText != null)
        {
            var sb = new System.Text.StringBuilder();
            var players = _lobby.Players;

            if (players == null || players.Count == 0)
            {
                sb.AppendLine("(sin jugadores)");
            }
            else
            {
                foreach (var p in players)
                {
                    string name = "Jugador";
                    string readyStr = "No listo";

                    if (p.Data != null)
                    {
                        if (p.Data.TryGetValue("name", out var nameObj) &&
                            !string.IsNullOrEmpty(nameObj.Value))
                            name = nameObj.Value;

                        if (p.Data.TryGetValue("ready", out var readyObj) &&
                            readyObj.Value == "true")
                            readyStr = "Listo";
                    }

                    //Debug.Log($"[RelayHUD] Player en lobby → name={name}, ready={readyStr}");
                    sb.AppendLine($"- {name} ({readyStr})");
                }
            }

            relayHudPlayersText.text = sb.ToString();
            //Debug.Log($"[RelayHUD] Texto pintado en HUD:\n{relayHudPlayersText.text}");
        }

        // --- Botón INICIAR JUEGO (HOST) ---
        if (relayHudStartGameButton != null)
        {
            relayHudStartGameButton.interactable = _isHost && AllRelayPlayersReady();
        }
    }


    bool AllRelayPlayersReady()
    {
        if (_lobby == null || _lobby.Players == null || _lobby.Players.Count == 0)
            return false;

        foreach (var p in _lobby.Players)
        {
            if (p.Data == null ||
                !p.Data.TryGetValue("ready", out var readyObj) ||
                readyObj.Value != "true")
            {
                return false;
            }
        }
        return true;
    }

    void Update()
    {
        // Actualizamos siempre los textos; internamente
        // UpdateLanHudTexts ya hace los checks de panel activo.
        UpdateLanHudTexts();
        UpdateRelayHudTexts();
        // Si no estamos en modo LAN no hacemos nada más.
        if (Mode != NetMode.LAN)
            return;

        var st = LanLobbyState.Instance;

        // Asegurarnos de estar suscritos a la lista de jugadores (solo una vez).
        EnsureLanLobbySubscription();

        // Si el juego ya empezó, ocultamos HUD y Canvas.
        if (st != null && st.GameStarted.Value)
        {
            if (panelLanHUD != null && panelLanHUD.activeSelf)
                panelLanHUD.SetActive(false);

            if (netModeCanvasRoot != null && netModeCanvasRoot.activeSelf)
                netModeCanvasRoot.SetActive(false);
        }
    }



    void DrawLanUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {

            // Try to create one on demand
            try
            {
                var nsType = System.Type.GetType("NetSetupOnce");
                if (nsType != null)
                {
                    var mi = nsType.GetMethod("EnsureNetworkManagerPublic", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (mi != null) mi.Invoke(null, null);
                }
            }
            catch { }

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
                try
                {
                    // Defensive re-init: ensure NetworkConfig exists and transport is assigned before starting
                    if (nm.NetworkConfig == null) nm.NetworkConfig = new Unity.Netcode.NetworkConfig();


                    // Escuchar en todas las interfaces
                    var utp = (UnityTransport)nm.NetworkConfig.NetworkTransport;
                    utp.SetConnectionData("0.0.0.0", lanPort, "0.0.0.0");

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
                }
                catch (System.Exception e) { Debug.LogError("Error creating host: " + e); _status = "No se pudo iniciar Host LAN (excepción)."; return; }

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
                bool shouldConnectRelayClient =
                    !_isHost &&
                    GetLobbyData("state") == "starting" &&
                    !NetworkManager.Singleton.IsListening;

                if (shouldConnectRelayClient)
                {
                    string code = GetLobbyData("joinCode");
                    Debug.Log($"[Relay] PollLobby detecta state='starting'. Intentando conectar cliente con joinCode={code}");

                    if (!string.IsNullOrEmpty(code))
                    {
                        _ = StartRelayClient(code);
                    }
                    else
                    {
                        Debug.LogWarning("[Relay] PollLobby: state='starting' pero joinCode vacío.");
                    }
                }
            }
            catch (Exception e)
            {
                _status = "Error en PollLobby: " + e.Message;
                Debug.LogWarning("[Lobby] PollLobby exception: " + e);
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
                Debug.Log("[Lobby] Requesting join code for allocation " + alloc.AllocationId + ".");
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

            if (panelRelayHUD != null)
                panelRelayHUD.SetActive(false);
            if (panelRelayLobby != null)
                panelRelayLobby.SetActive(false);
            if (panelNetMode != null)
                panelNetMode.SetActive(false);
            if (netModeCanvasRoot != null)
                netModeCanvasRoot.SetActive(false);

            // 🚀 NUEVO: una vez que el host Relay está activo, cargar la escena de juego
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[Relay] Cargando escena de juego: " + gameplaySceneName);
                NetworkManager.Singleton.SceneManager.LoadScene(
                    gameplaySceneName,
                    LoadSceneMode.Single
                );
            }
            else
            {
                Debug.LogWarning("[Relay] StartGame: NetworkManager no es server al intentar cargar escena.");
            }
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

        // Ensure Timer messaging and try to start/broadcast timer for Relay host
        try
        {
            // small delay to allow NetworkManager to settle
            await Task.Delay(200);
            EnsureTimerMessagingPresent();
            TryBroadcastTimerStartFromHost();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Relay] Could not trigger timer start after host: " + e);
        }
        // === Después de arrancar el Host por Relay, iniciar la partida igual que en LAN ===
        try
        {
            // Asegurarnos de que el objeto LanLobbyState existe
            EnsureLanStateSpawned();
            var state = LanLobbyState.Instance;

            if (state != null && state.IsServer)
            {
                Debug.Log("[Relay] Relay host listo, llamando a LanLobbyState.StartMatchAsHost().");
                state.StartMatchAsHost();
            }
            else
            {
                Debug.LogWarning("[Relay] No se pudo iniciar partida: LanLobbyState.Instance es null o no es server.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Relay] Error llamando a StartMatchAsHost después de StartRelayHost: " + e);
        }
    }


    async Task StartRelayClient(string joinCode)
    {
        // Ensure core services are initialized first
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
                    // Ensure TimerNetworkMessaging exists on the client so the named message handler is registered
                    try { EnsureTimerMessagingPresent(); Debug.Log("[Relay] Ensured TimerNetworkMessaging present on client."); } catch { }
                    if (nm.IsClient)
                    {
                        _status = "Cliente conectado por Relay.";
                        _hideHudRelay = true;
                        if (_pollCo != null) StopCoroutine(_pollCo); // opcional
                        Debug.Log("[Relay] StartRelayClient: nm.IsClient==true, client conectado por Netcode.");

                        // 👇 NUEVO: apagar el HUD del lobby también en el cliente
                        if (panelRelayHUD != null)
                            panelRelayHUD.SetActive(false);
                        if (panelRelayLobby != null)
                            panelRelayLobby.SetActive(false);
                        if (panelNetMode != null)
                            panelNetMode.SetActive(false);
                        if (netModeCanvasRoot != null)
                            netModeCanvasRoot.SetActive(false);
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

    // Helper: ensure TimerNetworkMessaging exists in scene (used by both LAN and Relay flows)
    void EnsureTimerMessagingPresent()
    {
        try
        {
            var tType = System.Type.GetType("TheLastKing.TimerNetworkMessaging, Assembly-CSharp");
            if (tType == null)
            {
                Debug.Log("[Lobby] TimerNetworkMessaging type not found in assembly.");
                return;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType(tType);
            if (existing != null) return;

            var go = new GameObject("TimerNetworkMessaging");
            go.AddComponent(tType);
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("[Lobby] TimerNetworkMessaging created and will persist across scenes.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Lobby] Could not ensure TimerNetworkMessaging: " + e);
        }
    }

    System.Collections.IEnumerator BroadcastStartWithRetries(int seconds)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var tType = System.Type.GetType("TheLastKing.TimerNetworkMessaging, Assembly-CSharp");
                if (tType != null)
                {
                    // try static method first
                    var mi = tType.GetMethod("BroadcastStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (mi != null)
                    {
                        mi.Invoke(null, new object[] { seconds });
                        Debug.Log($"[Lobby] BroadcastStart invoked (static) attempt {attempt}");
                        yield break;
                    }

                    // try instance method on existing object
                    var existing = UnityEngine.Object.FindAnyObjectByType(tType);
                    if (existing != null)
                    {
                        mi = tType.GetMethod("BroadcastStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (mi != null)
                        {
                            mi.Invoke(existing, new object[] { seconds });
                            Debug.Log($"[Lobby] BroadcastStart invoked (instance) attempt {attempt}");
                            yield break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Lobby] BroadcastStart attempt failed: " + e);
            }

            // wait a little and retry
            yield return new WaitForSeconds(0.25f * attempt);
        }

        Debug.LogWarning("[Lobby] BroadcastStartWithRetries: failed to invoke BroadcastStart after attempts.");
    }

    // Try to start the local host timer and broadcast start to connected clients (Relay path)
    void TryBroadcastTimerStartFromHost()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            // Prefer TimerStarter as the authoritative source for round duration.
            int seconds = 0;
            var ts = UnityEngine.Object.FindAnyObjectByType<TheLastKing.TimerStarter>();
            if (ts != null)
            {
                seconds = ts.roundDuration;
            }
            else
            {
                var ct = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
                if (ct != null) seconds = ct.durationSeconds;
            }

            // Fallback default if nothing provides a duration
            if (seconds <= 0) seconds = 60;

            // Update NetworkVariables in LanLobbyState if it exists (so late-joining clients see the timer)
            var lanState = LanLobbyState.Instance;
            if (lanState != null && lanState.IsServer)
            {
                lanState.TimerDuration.Value = seconds;
                lanState.TimerActive.Value = true;
                Debug.Log($"[Lobby] Updated LanLobbyState timer NetworkVariables: duration={seconds}, active=true");
            }

            // Start local host timer if possible. If there's no UI, create a runtime TimerUI as a fallback so the host always sees it.
            var ct2 = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (ct2 == null && ts == null)
            {
                // create runtime timer UI so host sees the sprite timer even if no prefab was placed in the scene
                try
                {
                    ct2 = CountdownTimerUI.CreateRuntimeTimerUI();
                    Debug.Log("[Lobby] Created runtime CountdownTimerUI for host fallback.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Lobby] Failed to create runtime CountdownTimerUI: " + e);
                }
            }

            if (ct2 != null)
            {
                try { ct2.EnsureAndStart(seconds); } catch { ct2.gameObject.SetActive(true); ct2.StartTimer(seconds); }
            }
            else if (ts != null)
            {
                try { ts.StartRound(); } catch { }
            }

            // Broadcast to clients via TimerNetworkMessaging.BroadcastStart (with retries)
            StartCoroutine(BroadcastStartWithRetries(seconds));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Lobby] TryBroadcastTimerStartFromHost failed: " + e);
        }
    }
}
