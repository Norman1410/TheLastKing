using UnityEngine;

public enum NetMode { None, LAN, Relay }    // <-- con estado "None" al inicio

public static class NetRuntime
{
    public static NetMode Mode = NetMode.None; // arranca sin elegir
}

public class NetModeSelector : MonoBehaviour
{
    void Awake() => DontDestroyOnLoad(gameObject);

    void OnGUI()
    {
        if (NetRuntime.Mode != NetMode.None) return;

        const int w = 260, h = 120;
        var rect = new Rect((Screen.width - w)/2, (Screen.height - h)/2, w, h);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("Seleccionar modo de red");
        if (GUILayout.Button("LAN (Local)"))  Choose(NetMode.LAN);
        if (GUILayout.Button("Relay (Internet)")) Choose(NetMode.Relay);
        GUILayout.EndArea();
    }

    void Choose(NetMode m)
    {
        NetRuntime.Mode = m;
        Destroy(gameObject);
    }
}
