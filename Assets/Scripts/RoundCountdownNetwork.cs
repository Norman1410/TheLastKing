using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class RoundCountdownNetwork : MonoBehaviour
{
    const string k_MessageName = "StartRoundCountdown";
    
    static int? pendingSeconds = null;

    void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;

        var cm = NetworkManager.Singleton.CustomMessagingManager;
        cm.RegisterNamedMessageHandler(k_MessageName, OnStartCountdownReceived);
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        var cm = NetworkManager.Singleton.CustomMessagingManager;
        cm.UnregisterNamedMessageHandler(k_MessageName);
    }

    public static void BroadcastCountdown(int seconds)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            Debug.Log("RoundCountdownNetwork: Solo el host puede enviar countdown.");
            return;
        }

        var cm = nm.CustomMessagingManager;

        foreach (var clientId in nm.ConnectedClientsIds)
        {
            if (clientId == nm.LocalClientId) continue;

            using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(seconds);
                cm.SendNamedMessage(k_MessageName, clientId, writer);
            }
        }

        Debug.Log($"RoundCountdownNetwork: Enviado countdown de {seconds}s a clientes.");
    }

    void OnStartCountdownReceived(ulong sender, FastBufferReader reader)
    {
        int seconds = 0;
        reader.ReadValueSafe(out seconds);

        Debug.Log($"RoundCountdownNetwork: Recibido countdown de {seconds}s");

        var ui = FindAnyObjectByType<RoundCountdownUI>();
        if (ui != null)
        {
            ui.StartCountdown(seconds);
        }
        else
        {
            pendingSeconds = seconds;
        }
    }

    void Update()
    {
        if (pendingSeconds.HasValue)
        {
            var ui = FindAnyObjectByType<RoundCountdownUI>();
            if (ui != null)
            {
                ui.StartCountdown(pendingSeconds.Value);
                pendingSeconds = null;
            }
        }
    }
}
