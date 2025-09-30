using UnityEngine;
using System.Threading.Tasks;

public class QuickRelayOverlay : MonoBehaviour
{
    Rect _rect = new Rect(10, 10, 360, 140);
    string _joinCodeInput = "";
    string _lastCode = "";
    string _status = "";
    bool _busy = false;

    void Awake() => DontDestroyOnLoad(gameObject);

    void OnGUI()
    {
        _rect = GUILayout.Window(999123, _rect, Draw, "Relay (UGS)");
    }

    void Draw(int id)
    {
        GUI.enabled = !_busy;
        if (GUILayout.Button("Host (Relay)")) _ = HostAsync();

        GUILayout.BeginHorizontal();
        _joinCodeInput = GUILayout.TextField(_joinCodeInput, GUILayout.MinWidth(140));
        if (GUILayout.Button("Join")) _ = JoinAsync(_joinCodeInput);
        GUILayout.EndHorizontal();

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_lastCode)) GUILayout.Label("Code: " + _lastCode);
        if (!string.IsNullOrEmpty(_status))    GUILayout.Label(_status);

        GUI.DragWindow();
    }

    async Task HostAsync()
    {
        _busy = true; _status = "Starting host...";
        try
        {
            var code = await RelayTransportBootstrap.StartHostWithRelayAsync(10);
            _lastCode = code;
            _status = "Host listo. Comparte el código.";
        }
        catch (System.Exception ex) { _status = "Error: " + ex.Message; }
        finally { _busy = false; }
    }

    async Task JoinAsync(string code)
    {
        _busy = true; _status = "Joining...";
        try
        {
            bool ok = await RelayTransportBootstrap.StartClientWithRelayAsync(code.Trim());
            _status = ok ? "¡Conectado!" : "Falló el Join.";
        }
        catch (System.Exception ex) { _status = "Error: " + ex.Message; }
        finally { _busy = false; }
    }
}
