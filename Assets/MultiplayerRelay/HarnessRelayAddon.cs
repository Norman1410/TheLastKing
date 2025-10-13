using UnityEngine;
using Unity.Netcode;

public static class HarnessRelayAddon
{
    // Solo corre si el modo elegido es Relay
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (NetRuntime.Mode != NetMode.Relay) return;

#if TLK_QUICKJOIN
        CreateRelayHUDAndTidy();
#endif
    }

    // Llamable manualmente desde el bootstrap unificado
    public static void TryCreateUI()
    {
        if (NetRuntime.Mode != NetMode.Relay) return;

#if TLK_QUICKJOIN
        CreateRelayHUDAndTidy();
#endif
    }

    static void CreateRelayHUDAndTidy()
    {
        // Si tu HUD LAN está activo, mátalo en unos frames (por si aparece tarde)
        var killerGO = new GameObject("LocalHudKiller");
        Object.DontDestroyOnLoad(killerGO);
        killerGO.AddComponent<LocalHudKiller>();

        // Crea UI Relay si no existe
        if (Object.FindObjectOfType<QuickRelayOverlay>() == null)
        {
            var ui = new GameObject("RelayOverlay_UI");
            Object.DontDestroyOnLoad(ui);
            ui.AddComponent<QuickRelayOverlay>();
        }

        // IMPORTANTE: NO apagar aquí la MainCamera.
        // Se apagará sola cuando el jugador dueño spawnee su cámara de red.
    }

    // Elimina HUD LAN si aparece mientras estamos en Relay
    class LocalHudKiller : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            for (int i = 0; i < 20; i++)
            {
                var hud = Object.FindObjectOfType<QuickJoinLocalUI>();
                if (hud != null) Object.Destroy(hud.gameObject);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
