using UnityEngine;

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

    // IMPORTANT: Disable automatic start
    [Tooltip("Disabled: Countdown system will trigger StartRound() manually.")]
    public bool autoStartWhenHostDetected = false;

    bool _hasAutoStarted = false;

    /// <summary>
    /// Called by your countdown system AFTER the 3-2-1 finishes.
    /// </summary>
    public void StartRound()
    {
        // If timer reference is missing, auto-find it
        if (timer == null)
        {
            var found = UnityEngine.Object.FindAnyObjectByType<CountdownTimerUI>();
            if (found != null)
            {
                timer = found;
                Debug.Log("TimerStarter: auto-assigned CountdownTimerUI from scene.");

                if (hideTimerUntilStart && timer.gameObject != null)
                    timer.gameObject.SetActive(false);

                try 
                {
                    UnityEngine.Object.DontDestroyOnLoad(timer.gameObject);
                    UnityEngine.Object.DontDestroyOnLoad(this.gameObject);
                } 
                catch { }
            }
            else
            {
                Debug.LogWarning("TimerStarter: No CountdownTimerUI found. Host will still broadcast.");
            }
        }

        if (requireHost && !IsHost())
        {
            Debug.Log("TimerStarter: StartRound ignored: not host.");
            return;
        }

        Debug.Log($"TimerStarter: Starting round as {(IsHost() ? "HOST" : "LOCAL")} | Duration: {roundDuration}s");

        if (timer != null)
        {
            if (hideTimerUntilStart && timer.gameObject != null)
                timer.gameObject.SetActive(true);

            timer.StartTimer(roundDuration);
        }
        else
        {
            Debug.LogWarning("TimerStarter: UI not found. Broadcasting only.");
        }

        // HOST broadcasts timer start
        try
        {
            if (Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                var typeName = "TheLastKing.TimerNetworkMessaging, Assembly-CSharp";
                var tType = System.Type.GetType(typeName);

                if (tType != null)
                {
                    var mi = tType.GetMethod("BroadcastStart",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (mi != null)
                        mi.Invoke(null, new object[] { roundDuration });
                    else
                        Debug.LogWarning("TimerStarter: BroadcastStart method not found.");
                }
                else
                {
                    Debug.LogWarning("TimerStarter: TimerNetworkMessaging type not found.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("TimerStarter: Error broadcasting start: " + ex.Message);
        }
    }

    bool IsHost()
    {
        if (isHostOverrideForTesting)
            return true;

        try
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null)
                return nm.IsHost;
        }
        catch { }

        return false;
    }

    // Disabled: Automatic round start removed
    void Update()
    {
        // AUTO-START REMOVED ON PURPOSE
        // Your countdown system will decide when StartRound() is called.
    }
}

}
