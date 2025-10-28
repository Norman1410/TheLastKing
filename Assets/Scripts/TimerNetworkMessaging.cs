using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Usa CustomMessagingManager para anunciar a los clientes que el host inicia la ronda.
/// - Registra un handler "StartTimer" que recibe un int (seconds) y arranca el timer local.
/// - El host puede llamar a BroadcastStart(seconds) para enviar el mensaje a todos los clientes.
/// </summary>
namespace TheLastKing {

public class TimerNetworkMessaging : MonoBehaviour
{
    const string k_MessageName = "StartTimer";
    const string k_TimeSyncRequest = "TimeSyncRequest";
    const string k_TimeSyncResponse = "TimeSyncResponse";
    const string k_TimerStateRequest = "TimerStateRequest";
    const string k_TimerStateResponse = "TimerStateResponse";

    // If a StartTimer message arrives before the local CountdownTimerUI exists
    // we store the requested seconds here and try again when a scene is loaded or in Update.
    static int? s_pendingStartSeconds = null;
    static long? s_pendingStartServerUtcMs = null;
    // Estimated clock offset (serverUtc - clientUtc) in milliseconds. Positive means server time is ahead of client time.
    static double s_estimatedClockOffsetMs = 0.0;
    static bool s_timeSyncCompleted = false;
    // Server-side authoritative timer state (used to answer late-join queries)
    static bool s_serverTimerActive = false;
    static int s_serverTimerDuration = 0;
    static long s_serverTimerStartUtcMs = 0;
    bool _registered = false;
    // Fallback textual timer if no sprite-based UI exists
    GameObject _fallbackTimerGO;
    Text _fallbackTimerText;
    Coroutine _fallbackCoroutine;

    void Update()
    {
        // Ensure we register the named message handler as soon as NetworkManager becomes available
        if (!_registered && NetworkManager.Singleton != null)
        {
            try
            {
                var cm = NetworkManager.Singleton.CustomMessagingManager;
                cm.RegisterNamedMessageHandler(k_MessageName, OnStartTimerReceived);
                cm.RegisterNamedMessageHandler(k_TimeSyncResponse, OnTimeSyncResponse);
                cm.RegisterNamedMessageHandler(k_TimerStateResponse, OnTimerStateResponse);
                // server side also listens for TimeSyncRequest and TimerStateRequest
                if (NetworkManager.Singleton.IsServer)
                {
                    cm.RegisterNamedMessageHandler(k_TimeSyncRequest, OnTimeSyncRequest);
                    cm.RegisterNamedMessageHandler(k_TimerStateRequest, OnTimerStateRequest);
                }

                SceneManager.sceneLoaded += OnSceneLoaded;
                _registered = true;
                Debug.Log("TimerNetworkMessaging: Registered named message handlers (late)");

                // If this is a client, kick off a time-sync to measure offset
                if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    // send a time sync request after a short delay to let connection settle
                    StartCoroutine(TimeSyncRequestWithDelay(0.1f));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("TimerNetworkMessaging: failed late register: " + ex);
            }
        }

        if (s_pendingStartSeconds.HasValue)
        {
            var t = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (t != null)
            {
                int seconds = s_pendingStartSeconds.Value;
                long serverUtcMs = s_pendingStartServerUtcMs ?? 0;
                s_pendingStartSeconds = null;
                s_pendingStartServerUtcMs = null;
                Debug.Log($"TimerNetworkMessaging: applying pending StartTimer({seconds}) after UI appeared (serverUtcMs={serverUtcMs})");
                try
                {
                    if (serverUtcMs > 0)
                    {
                        long clientUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
                        int elapsedSec = Mathf.Max(0, Mathf.RoundToInt((clientUtcMs - serverUtcMs) / 1000f));
                        int remaining = Mathf.Max(0, seconds - elapsedSec);
                        t.EnsureAndStart(remaining);
                    }
                    else
                    {
                        t.EnsureAndStart(seconds);
                    }
                }
                catch
                {
                    t.gameObject.SetActive(true);
                    t.StartTimer(seconds);
                }
            }
            else
            {
                // start fallback textual timer so player sees something
                int seconds = s_pendingStartSeconds.Value;
                long serverUtcMs = s_pendingStartServerUtcMs ?? 0;
                s_pendingStartSeconds = null;
                s_pendingStartServerUtcMs = null;
                Debug.Log($"TimerNetworkMessaging: no CountdownTimerUI yet; starting fallback timer {seconds}s (serverUtcMs={serverUtcMs})");
                StartFallbackTimer(seconds);
            }
        }
    }

    void OnEnable()
    {
        // Try immediate register; if NetworkManager isn't ready yet Update() will attempt late registration
        if (NetworkManager.Singleton != null)
        {
            try
            {
                var cm = NetworkManager.Singleton.CustomMessagingManager;
                cm.RegisterNamedMessageHandler(k_MessageName, OnStartTimerReceived);
                cm.RegisterNamedMessageHandler(k_TimeSyncResponse, OnTimeSyncResponse);
                cm.RegisterNamedMessageHandler(k_TimerStateResponse, OnTimerStateResponse);
                if (NetworkManager.Singleton.IsServer)
                {
                    cm.RegisterNamedMessageHandler(k_TimeSyncRequest, OnTimeSyncRequest);
                    cm.RegisterNamedMessageHandler(k_TimerStateRequest, OnTimerStateRequest);
                }
                SceneManager.sceneLoaded += OnSceneLoaded;
                _registered = true;

                if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    StartCoroutine(TimeSyncRequestWithDelay(0.1f));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("TimerNetworkMessaging: failed to register on enable: " + ex);
            }
        }
    }

    void OnDisable()
    {
        if (_registered && NetworkManager.Singleton != null)
        {
            try
            {
                var cm = NetworkManager.Singleton.CustomMessagingManager;
                cm.UnregisterNamedMessageHandler(k_MessageName);
                cm.UnregisterNamedMessageHandler(k_TimeSyncResponse);
                cm.UnregisterNamedMessageHandler(k_TimerStateResponse);
                if (NetworkManager.Singleton.IsServer)
                {
                    cm.UnregisterNamedMessageHandler(k_TimeSyncRequest);
                    cm.UnregisterNamedMessageHandler(k_TimerStateRequest);
                }
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _registered = false;
            }
            catch { }
        }
    }

    System.Collections.IEnumerator TimeSyncRequestWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SendTimeSyncRequest();
    }

    void SendTimeSyncRequest()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient) return;
            var cm = nm.CustomMessagingManager;
            long clientUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            using (var writer = new FastBufferWriter(sizeof(long), Allocator.Temp))
            {
                writer.WriteValueSafe(clientUtcMs);
                // send to server (ServerClientId)
                cm.SendNamedMessage(k_TimeSyncRequest, NetworkManager.ServerClientId, writer);
            }
            Debug.Log($"TimerNetworkMessaging: Sent TimeSyncRequest (clientUtcMs={clientUtcMs}) to server");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: SendTimeSyncRequest failed: " + e);
        }
    }

    // Server handler: receives client's request and replies with serverUtcMs and echoes client's timestamp
    void OnTimeSyncRequest(ulong clientId, FastBufferReader reader)
    {
        try
        {
            long clientUtcMs = 0;
            reader.ReadValueSafe(out clientUtcMs);
            long serverUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            var cm = NetworkManager.Singleton.CustomMessagingManager;
            using (var writer = new FastBufferWriter(sizeof(long) + sizeof(long), Allocator.Temp))
            {
                writer.WriteValueSafe(serverUtcMs);
                writer.WriteValueSafe(clientUtcMs);
                cm.SendNamedMessage(k_TimeSyncResponse, clientId, writer);
            }
            Debug.Log($"TimerNetworkMessaging: Replied TimeSyncResponse to client {clientId} (serverUtcMs={serverUtcMs}, clientUtcMs={clientUtcMs})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: OnTimeSyncRequest failed: " + e);
        }
    }

    // Client handler: receives serverUtcMs and echoed clientUtcMs, compute RTT and offset
    void OnTimeSyncResponse(ulong senderClientId, FastBufferReader reader)
    {
        try
        {
            long serverUtcMs = 0;
            long clientSentUtcMs = 0;
            reader.ReadValueSafe(out serverUtcMs);
            reader.ReadValueSafe(out clientSentUtcMs);
            long clientRecvUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            long rtt = clientRecvUtcMs - clientSentUtcMs;
            double offset = (double)serverUtcMs - (double)(clientSentUtcMs + rtt / 2.0);
            s_estimatedClockOffsetMs = offset;
            s_timeSyncCompleted = true;
            Debug.Log($"TimerNetworkMessaging: TimeSyncResponse received. RTT={rtt}ms, offset={offset}ms");
            // After time sync, request current timer state from server so late-joining clients catch up
            try { StartCoroutine(SendTimerStateRequestWithDelay(0.05f)); } catch { SendTimerStateRequest(); }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: OnTimeSyncResponse failed: " + e);
        }
    }

    void OnStartTimerReceived(ulong clientId, FastBufferReader reader)
    {
        int seconds = 0;
        long serverUtcMs = 0;
        reader.ReadValueSafe(out seconds);
        // Try to read server timestamp if present (older messages may not include it)
        try { reader.ReadValueSafe(out serverUtcMs); } catch { serverUtcMs = 0; }
        Debug.Log($"TimerNetworkMessaging: received StartTimer({seconds}) from server (serverUtcMs={serverUtcMs})");
        var t = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
        
        // If no UI exists, create one at runtime so client always sees the sprite timer
        if (t == null)
        {
            Debug.Log("TimerNetworkMessaging: No CountdownTimerUI found on client, creating runtime UI...");
            try
            {
                t = CountdownTimerUI.CreateRuntimeTimerUI();
                Debug.Log("TimerNetworkMessaging: Runtime CountdownTimerUI created for client.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("TimerNetworkMessaging: Failed to create runtime UI: " + ex);
            }
        }
        
        if (t != null)
        {
            // If server provided a timestamp, compute elapsed and adjust remaining
            int startSeconds = seconds;
            if (serverUtcMs > 0)
            {
                long clientUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
                long elapsedMs = clientUtcMs - serverUtcMs;
                int elapsedSec = Mathf.Max(0, Mathf.RoundToInt(elapsedMs / 1000f));
                int remaining = Mathf.Max(0, startSeconds - elapsedSec);
                Debug.Log($"TimerNetworkMessaging: adjusted by elapsed={elapsedSec}s -> remaining={remaining}s");
                try
                {
                    t.EnsureAndStart(remaining);
                    Debug.Log($"TimerNetworkMessaging: Client timer UI started with sprites for {remaining}s (adjusted)");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("TimerNetworkMessaging: EnsureAndStart failed: " + e);
                    t.gameObject.SetActive(true);
                    t.StartTimer(remaining);
                }
            }
            else
            {
                // Use EnsureAndStart to force sprite loading and wiring on client
                try
                {
                    t.EnsureAndStart(seconds);
                    Debug.Log($"TimerNetworkMessaging: Client timer UI started with sprites for {seconds}s");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("TimerNetworkMessaging: EnsureAndStart failed: " + e);
                    t.gameObject.SetActive(true);
                    t.StartTimer(seconds);
                }
            }
        }
        else
        {
            // UI creation failed. Start a fallback textual timer
            Debug.Log("TimerNetworkMessaging: CountdownTimerUI creation failed - starting fallback timer");
            StartFallbackTimer(seconds);
            // also store pending in case a proper UI appears later (store server timestamp if present)
            s_pendingStartSeconds = seconds;
            if (serverUtcMs > 0) s_pendingStartServerUtcMs = serverUtcMs;
        }
    }

    System.Collections.IEnumerator SendTimerStateRequestWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SendTimerStateRequest();
    }

    void SendTimerStateRequest()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient) return;
            var cm = nm.CustomMessagingManager;
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                // no payload required
                cm.SendNamedMessage(k_TimerStateRequest, NetworkManager.ServerClientId, writer);
            }
            Debug.Log("TimerNetworkMessaging: Sent TimerStateRequest to server");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: SendTimerStateRequest failed: " + e);
        }
    }

    // Server handler: reply with current timer state
    void OnTimerStateRequest(ulong clientId, FastBufferReader reader)
    {
        try
        {
            var cm = NetworkManager.Singleton.CustomMessagingManager;
            using (var writer = new FastBufferWriter(sizeof(byte) + sizeof(int) + sizeof(long), Allocator.Temp))
            {
                byte active = (byte)(s_serverTimerActive ? 1 : 0);
                writer.WriteValueSafe(active);
                writer.WriteValueSafe(s_serverTimerDuration);
                writer.WriteValueSafe(s_serverTimerStartUtcMs);
                cm.SendNamedMessage(k_TimerStateResponse, clientId, writer);
            }
            Debug.Log($"TimerNetworkMessaging: Replied TimerStateResponse to {clientId} (active={s_serverTimerActive}, duration={s_serverTimerDuration}, startUtc={s_serverTimerStartUtcMs})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: OnTimerStateRequest failed: " + e);
        }
    }

    // Client handler: receive server timer state and adjust/start local timer
    void OnTimerStateResponse(ulong senderClientId, FastBufferReader reader)
    {
        try
        {
            byte activeB = 0;
            int duration = 0;
            long startUtcMs = 0;
            reader.ReadValueSafe(out activeB);
            reader.ReadValueSafe(out duration);
            reader.ReadValueSafe(out startUtcMs);
            bool active = activeB != 0;
            Debug.Log($"TimerNetworkMessaging: Received TimerStateResponse active={active}, duration={duration}, startUtc={startUtcMs}");
            if (!active) return;

            // Compute remaining using clock offset if available
            long clientUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            double serverNowEstimate = (double)clientUtcMs + s_estimatedClockOffsetMs;
            double elapsedSec = (serverNowEstimate - (double)startUtcMs) / 1000.0;
            int remaining = Mathf.Max(0, duration - Mathf.FloorToInt((float)elapsedSec));
            Debug.Log($"TimerNetworkMessaging: TimerStateResponse computed remaining={remaining}s (elapsed={elapsedSec}s)");

            var ct = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (ct == null)
            {
                try { ct = CountdownTimerUI.CreateRuntimeTimerUI(); } catch { }
            }
            if (ct != null)
            {
                try { ct.EnsureAndStart(remaining); }
                catch { ct.gameObject.SetActive(true); ct.StartTimer(remaining); }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TimerNetworkMessaging: OnTimerStateResponse failed: " + e);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When a new scene is loaded, try to apply any pending start
        if (!s_pendingStartSeconds.HasValue) return;
        var t = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
        if (t != null)
        {
            int seconds = s_pendingStartSeconds.Value;
            long serverUtcMs = s_pendingStartServerUtcMs ?? 0;
            s_pendingStartSeconds = null;
            s_pendingStartServerUtcMs = null;
            Debug.Log($"TimerNetworkMessaging: applying pending StartTimer({seconds}) on sceneLoaded '{scene.name}' (serverUtcMs={serverUtcMs})");
            try
            {
                if (serverUtcMs > 0)
                {
                    long clientUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
                    int elapsedSec = Mathf.Max(0, Mathf.RoundToInt((clientUtcMs - serverUtcMs) / 1000f));
                    int remaining = Mathf.Max(0, seconds - elapsedSec);
                    t.EnsureAndStart(remaining);
                }
                else
                {
                    t.EnsureAndStart(seconds);
                }
            }
            catch
            {
                t.gameObject.SetActive(true);
                t.StartTimer(seconds);
            }
        }
        else
        {
            // fallback
            int seconds = s_pendingStartSeconds.Value;
            s_pendingStartSeconds = null;
            s_pendingStartServerUtcMs = null;
            StartFallbackTimer(seconds);
        }
    }

    public void StartFallbackTimer(int seconds)
    {
        if (_fallbackCoroutine != null)
        {
            StopCoroutine(_fallbackCoroutine);
            _fallbackCoroutine = null;
        }

        if (_fallbackTimerGO == null)
            CreateFallbackTimerUI();

        if (_fallbackTimerText != null)
            _fallbackCoroutine = StartCoroutine(FallbackTimerCoroutine(seconds));
    }

    void CreateFallbackTimerUI()
    {
        // Create a simple Canvas + Text anchored top center
    var existingCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
    GameObject canvasGO = existingCanvas != null ? existingCanvas.gameObject : null;
        if (canvasGO == null)
        {
            canvasGO = new GameObject("TLK_TimerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGO.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = canvasGO.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            UnityEngine.Object.DontDestroyOnLoad(canvasGO);
        }

        _fallbackTimerGO = new GameObject("TimerFallbackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        _fallbackTimerGO.transform.SetParent(canvasGO.transform, false);
        var rt = _fallbackTimerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(400, 100);

        _fallbackTimerText = _fallbackTimerGO.GetComponent<Text>();
        _fallbackTimerText.alignment = TextAnchor.MiddleCenter;
        _fallbackTimerText.fontSize = 48;
        _fallbackTimerText.color = Color.white;
    _fallbackTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _fallbackTimerText.text = "--:--";

        UnityEngine.Object.DontDestroyOnLoad(_fallbackTimerGO);
    }

    System.Collections.IEnumerator FallbackTimerCoroutine(int seconds)
    {
        int remaining = seconds;
        _fallbackTimerGO.SetActive(true);
        while (remaining >= 0)
        {
            int m = remaining / 60;
            int s = remaining % 60;
            _fallbackTimerText.text = string.Format("{0:00}:{1:00}", m, s);
            yield return new WaitForSeconds(1f);
            remaining--;
        }
        // hide after finish
        _fallbackTimerGO.SetActive(false);
    }

    /// <summary>
    /// Envia a todos los clientes conectados (excepto al host local) el mensaje StartTimer(seconds).
    /// Debe ser llamado desde el host (server).
    /// </summary>
    public static void BroadcastStart(int seconds)
    {
        // Simple overload: include server UTC milliseconds now
        long serverUtcMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
        BroadcastStartInternal(seconds, serverUtcMs);
    }

    static void BroadcastStartInternal(int seconds, long serverUtcMs)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("TimerNetworkMessaging: NetworkManager.Singleton == null, no se puede enviar mensaje");
            return;
        }
        if (!nm.IsServer)
        {
            Debug.LogWarning("TimerNetworkMessaging: BroadcastStart debe llamarse desde el servidor/host");
            return;
        }

    // Update server-side authoritative timer state so late-join queries can be answered
    s_serverTimerActive = true;
    s_serverTimerDuration = seconds;
    s_serverTimerStartUtcMs = serverUtcMs;

    // Ask LanLobbyState to start server-side authoritative timer so round-end logic runs server-side
    try
    {
        var lan = UnityEngine.Object.FindAnyObjectByType<LanLobbyState>();
        if (lan != null)
        {
            lan.StartServerTimerPublic(seconds);
            Debug.Log($"TimerNetworkMessaging: Requested LanLobbyState to start server timer for {seconds}s");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogWarning("TimerNetworkMessaging: Failed to request LanLobbyState to start server timer: " + e);
    }

    var cm = nm.CustomMessagingManager;
        int clientCount = 0;
        foreach (var clientId in nm.ConnectedClientsIds)
        {
            if (clientId == nm.LocalClientId) continue; // skip host local
            using (var writer = new FastBufferWriter(sizeof(int) + sizeof(long), Allocator.Temp))
            {
                writer.WriteValueSafe(seconds);
                writer.WriteValueSafe(serverUtcMs);
                cm.SendNamedMessage(k_MessageName, clientId, writer);
                clientCount++;
                Debug.Log($"TimerNetworkMessaging: Sent StartTimer({seconds}, serverUtcMs={serverUtcMs}) to client {clientId}");
            }
        }
        Debug.Log($"TimerNetworkMessaging: BroadcastStart({seconds}) sent to {clientCount} clients (total connected: {nm.ConnectedClientsIds.Count})");
    }
    
    /// <summary>
    /// Broadcast with retry - waits for clients to connect and retries sending the start message.
    /// Useful for host to ensure late-joining clients also receive the timer start.
    /// </summary>
    public static void BroadcastStartWithRetry(int seconds, MonoBehaviour caller, int maxRetries = 5, float delayBetweenRetries = 0.5f)
    {
        if (caller != null)
            caller.StartCoroutine(BroadcastStartCoroutine(seconds, maxRetries, delayBetweenRetries));
        else
            BroadcastStart(seconds);
    }

    static System.Collections.IEnumerator BroadcastStartCoroutine(int seconds, int maxRetries, float delay)
    {
        int attempt = 0;
        while (attempt < maxRetries)
        {
            attempt++;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
            {
                int clientCount = nm.ConnectedClientsIds.Count - 1; // exclude host
                if (clientCount > 0)
                {
                    Debug.Log($"TimerNetworkMessaging: BroadcastStartCoroutine attempt {attempt} - sending to {clientCount} clients");
                    BroadcastStart(seconds);
                    yield break; // success, stop retrying
                }
                else
                {
                    Debug.Log($"TimerNetworkMessaging: BroadcastStartCoroutine attempt {attempt} - no clients connected yet, retrying...");
                }
            }
            yield return new WaitForSeconds(delay);
        }
        Debug.LogWarning($"TimerNetworkMessaging: BroadcastStartCoroutine gave up after {maxRetries} attempts - no clients found");
    }

}

}
