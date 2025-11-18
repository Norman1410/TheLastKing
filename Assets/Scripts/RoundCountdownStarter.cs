using UnityEngine;

public class RoundCountdownStarter : MonoBehaviour
{
    public RoundCountdownUI countdownUI;
    public int countdownSeconds = 3;

    public void StartPreRoundCountdown()
    {
        Debug.Log("🔥 RoundCountdownStarter: StartPreRoundCountdown() WAS CALLED.");

        bool isHost = Unity.Netcode.NetworkManager.Singleton != null &&
                      Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (!isHost)
        {
            Debug.Log("RoundCountdownStarter: Solo el host inicia el countdown.");
            return;
        }

        // Mostrar countdown localmente
        if (countdownUI != null)
            countdownUI.StartCountdown(countdownSeconds, BeginRound);

        // Enviar countdown a los clientes
        RoundCountdownNetwork.BroadcastCountdown(countdownSeconds);
    }

    void BeginRound()
    {
        Debug.Log("🔥 RoundCountdownStarter: COUNTDOWN FINISHED. Calling StartRound()");
        Debug.Log("RoundCountdownStarter: Countdown terminado → iniciar ronda real.");
        
        // Aquí llamas al script del compañero
        FindAnyObjectByType<TheLastKing.TimerStarter>()?.StartRound();
    }
}
