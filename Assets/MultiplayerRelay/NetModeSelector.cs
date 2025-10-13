using UnityEngine;

public enum NetMode { None, LAN, Relay }

public static class NetRuntime
{
    public static NetMode Mode = NetMode.None;
}

public class NetModeSelector : MonoBehaviour
{
    void Awake()
    {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        var hasAnchor = FindAnyObjectByType<NetUIAnchor>(FindObjectsInactive.Include) != null;
        var hasLanLobby = FindAnyObjectByType<LanLobbyState>(FindObjectsInactive.Include) != null;
#else
        var hasAnchor = FindObjectOfType<NetUIAnchor>(true) != null;
        var hasLanLobby = FindObjectOfType<LanLobbyState>(true) != null;
#endif

        // Also allow selector when the active scene is the gameplay scene
        bool isGameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Pruebas_TheLastKing";

        bool allowed = hasAnchor || hasLanLobby || isGameScene;

        if (!allowed)
        {
            Destroy(gameObject);
            return;
        }

        // ❌ No uses DontDestroyOnLoad: el selector pertenece a la escena que lo contiene.
        // DontDestroyOnLoad(gameObject);
    }

    void OnGUI()
    {
        if (NetRuntime.Mode != NetMode.None) return;

        const int w = 260, h = 120;
        var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);

        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("Seleccionar modo de red");
        if (GUILayout.Button("LAN (Local)"))   Choose(NetMode.LAN);
        if (GUILayout.Button("Relay (Internet)")) Choose(NetMode.Relay);
        GUILayout.EndArea();
    }

    void Choose(NetMode m)
    {
        NetRuntime.Mode = m;
        Destroy(gameObject);
    }
}
