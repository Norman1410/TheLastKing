using UnityEngine;

public static class UnifiedBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (NetRuntime.Mode == NetMode.None)
        {
            // Crea selector y suprime HUD LAN hasta que elijan
            if (Object.FindObjectOfType<NetModeSelector>() == null)
            {
                var sel = new GameObject("NetModeSelector");
                Object.DontDestroyOnLoad(sel);
                sel.AddComponent<NetModeSelector>();
            }
            var sup = new GameObject("LanHudSuppressor");
            Object.DontDestroyOnLoad(sup);
            sup.AddComponent<LanHudSuppressor>();

            CoroutineRunner.Run(DelayedStart()); // espera elección
        }
        else
        {
            // El menú ya eligió → arranca directo
            CoroutineRunner.Run(DirectStart());
        }
    }

    static System.Collections.IEnumerator DelayedStart()
    {
        while (NetRuntime.Mode == NetMode.None) yield return null; // esperar elección
        yield return DirectStart();
    }

    static System.Collections.IEnumerator DirectStart()
    {
        if (NetRuntime.Mode == NetMode.Relay)
        {
            RelayTransportBootstrap.EnsureUGSAsync().Forget(); // opcional (pre-login)
            HarnessRelayAddon.TryCreateUI();                   // crea HUD Relay
        }
        else if (NetRuntime.Mode == NetMode.LAN)
        {
            LanHarnessBootstrap.InitIfNeeded();               // crea/activa HUD LAN
        }
        yield break;
    }

    // --- helpers ---
    class CoroutineRunner : MonoBehaviour
    {
        public static void Run(System.Collections.IEnumerator r)
        {
            var go = new GameObject("BootstrapRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CoroutineRunner>().StartCoroutine(r);
        }
    }
}

// Mientras no se elija modo, mantener el HUD LAN oculto
class LanHudSuppressor : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        while (NetRuntime.Mode == NetMode.None)
        {
            var hud = Object.FindObjectOfType<QuickJoinLocalUI>(true); // tu HUD LAN
            if (hud != null) hud.gameObject.SetActive(false);
            yield return null;
        }
        Destroy(gameObject);
    }
}

static class TaskExt
{
    public static async void Forget(this System.Threading.Tasks.Task t)
    {
        try { await t; } catch { }
    }
}
