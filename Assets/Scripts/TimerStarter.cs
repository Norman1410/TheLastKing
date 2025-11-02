using UnityEngine;

/// <summary>
/// Helper para arrancar el cronómetro desde el Host (o manualmente para pruebas).
/// - Añade este componente a tu GameManager o al mismo TimerUI.
/// - Asigna el CountdownTimerUI en el inspector y el tiempo de ronda.
/// - Conecta el botón "Start Game" del host al método StartRound() de este componente.
///
/// Nota: este script NO depende de una librería de red específica. Edita IsHost() para integrar
/// la comprobación de host de tu sistema de networking (Mirror / Netcode / Photon).
/// </summary>
namespace TheLastKing {

public class TimerStarter : MonoBehaviour
{
    [Tooltip("Referencia al componente CountdownTimerUI (TimerUI GameObject)")]
    public CountdownTimerUI timer;

    [Tooltip("Duración de la ronda en segundos")]
    public int roundDuration = 60;

    [Tooltip("Si está activado, solo el host podrá iniciar la ronda (por defecto true en producción)")]
    public bool requireHost = true;

    [Tooltip("Override manual para tests en editor: si true permite iniciar incluso si IsHost() devuelve false")]
    public bool isHostOverrideForTesting = false;

    [Tooltip("If true, the Timer GameObject will be hidden until StartRound is called")]
    public bool hideTimerUntilStart = true;

    [Tooltip("If true, automatically start the round when this instance becomes Host (useful because LobbyController starts the host).")]
    public bool autoStartWhenHostDetected = true;

    bool _hasAutoStarted = false;

    /// <summary>
    /// Llamar desde el botón "Start Game" (OnClick) o desde tu código del Host cuando estés listo.
    /// </summary>
    public void StartRound()
    {
        // If timer reference is missing, try to auto-find it in the scene
        if (timer == null)
        {
            var found = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (found != null)
            {
                timer = found;
                Debug.Log("TimerStarter: auto-assigned CountdownTimerUI from scene.");
                // If configured to hide until start, ensure it's hidden before we activate it below
                if (hideTimerUntilStart && timer.gameObject != null) timer.gameObject.SetActive(false);
                try { UnityEngine.Object.DontDestroyOnLoad(timer.gameObject); UnityEngine.Object.DontDestroyOnLoad(this.gameObject); } catch { }
            }
            else
            {
                Debug.LogWarning("TimerStarter: no hay referencia a CountdownTimerUI asignada y no se encontró en la escena. Host seguirá broadcast pero no mostrará UI localmente.");
                // continue: still broadcast to clients even if local UI missing
            }
        }

        if (requireHost && !IsHost())
        {
            Debug.Log("TimerStarter: StartRound ignorado: no es host.");
            return;
        }

        Debug.Log($"TimerStarter: iniciando ronda por { (IsHost() ? "HOST" : "LOCAL/OVERRIDE") }. Duración: {roundDuration}s");
        if (timer != null)
        {
            if (hideTimerUntilStart && timer.gameObject != null)
            {
                timer.gameObject.SetActive(true);
            }
            timer.StartTimer(roundDuration);
        }
        else
        {
            Debug.LogWarning("TimerStarter: no se pudo iniciar UI local (CountdownTimerUI es null). Se hará broadcast para clientes.");
        }

        // If we are host/server, broadcast start to clients so they can start their timers too.
        try
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                // Broadcast to clients so they can start their timers too
                // Use reflection to avoid compile-time dependency issues in some compilation orders
                var typeName = "TheLastKing.TimerNetworkMessaging, Assembly-CSharp";
                var tType = System.Type.GetType(typeName);
                if (tType != null)
                {
                    var mi = tType.GetMethod("BroadcastStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (mi != null) mi.Invoke(null, new object[] { roundDuration });
                    else Debug.LogWarning("TimerStarter: BroadcastStart method not found via reflection.");
                }
                else
                {
                    Debug.LogWarning("TimerStarter: TimerNetworkMessaging type not found via reflection. Clients won't be notified.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("TimerStarter: error broadcasting timer start: " + ex.Message);
        }
    }

    /// <summary>
    /// Comprueba si este jugador es el host. Reemplaza/ajusta este método según la librería de networking que uses.
    /// Ejemplos (descomentar y usar según tu stack):
    /// Mirror: return Mirror.NetworkServer.active;
    /// Netcode (Unity): return Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;
    /// Photon PUN: return Photon.Pun.PhotonNetwork.IsMasterClient;
    /// </summary>
    bool IsHost()
    {
        if (isHostOverrideForTesting) return true; // útil para pruebas locales

        // Por defecto detectamos host usando Netcode for GameObjects (Unity.Netcode) ya que tu proyecto lo usa.
        try
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null) return nm.IsHost;
        }
        catch { }

        // Si usas Photon o Mirror, reemplaza/añade comprobaciones aquí según sea necesario.
        // Mirror example: Mirror.NetworkServer.active
        // Photon example: Photon.Pun.PhotonNetwork.IsMasterClient

        return false;
    }

    // Útil para poder llamar desde código y forzar inicio (por ejemplo el host manda RPC y en clientes se llama StartRoundForced)
    public void StartRoundForced()
    {
        if (timer == null) return;
        if (hideTimerUntilStart && timer.gameObject != null) timer.gameObject.SetActive(true);
        timer.StartTimer(roundDuration);
    }

    // Método para tests rápidos desde el inspector: menú contextual
    [ContextMenu("Test Start Round (editor override)")]
    void TestStart()
    {
        bool prev = isHostOverrideForTesting;
        isHostOverrideForTesting = true;
        StartRound();
        isHostOverrideForTesting = prev;
    }

    void Awake()
    {
        // hide timer until start if desired
        if (timer == null)
        {
            // try to auto-find the CountdownTimerUI in the scene so we don't depend on inspector wiring
            var found = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (found != null)
            {
                timer = found;
                Debug.Log("TimerStarter: auto-assigned CountdownTimerUI in Awake.");
            }
        }

        if (hideTimerUntilStart && timer != null && timer.gameObject != null)
        {
            timer.gameObject.SetActive(false);
            // Make timer persist across scene loads so it will be available in the gameplay scene
            try
            {
                UnityEngine.Object.DontDestroyOnLoad(timer.gameObject);
                UnityEngine.Object.DontDestroyOnLoad(this.gameObject);
            }
            catch { }
        }
    }

    void Update()
    {
        if (autoStartWhenHostDetected && !_hasAutoStarted && requireHost && IsHost())
        {
            _hasAutoStarted = true;
            StartRound();
        }
    }
}

}
