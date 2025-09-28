using Unity.Netcode;
using UnityEngine;
using System.Reflection;

public class QuickJoinLocalUI : MonoBehaviour
{
    Rect r = new Rect(10, 10, 260, 95);

    void OnGUI()
    {
        GUILayout.BeginArea(r, GUI.skin.box);
        GUILayout.Label("MP Harness (Local)");

        if (GUILayout.Button("Host (127.0.0.1:7777)"))
        {
            var nm = NetworkManager.Singleton ?? FindNM();
            if (!PrepareConfig(nm)) return;
            nm.StartHost();
        }

        if (GUILayout.Button("Client (Join local)"))
        {
            var nm = NetworkManager.Singleton ?? FindNM();
            if (!PrepareConfig(nm)) return;
            nm.StartClient();
        }

        GUILayout.EndArea();
    }

    NetworkManager FindNM()
    {
        var nm = Object.FindObjectOfType<NetworkManager>();
        if (nm == null) Debug.LogError("[UI] No hay NetworkManager en la escena.");
        return nm;
    }

    bool PrepareConfig(NetworkManager nm)
    {
        if (nm == null) return false;

        // Asegura que haya NetworkConfig
        if (nm.NetworkConfig == null)
            nm.NetworkConfig = new NetworkConfig();

        // Apaga Main Camera de escena si no es de red
        var main = Camera.main;
        if (main != null && main.transform.GetComponentInParent<NetworkObject>() == null)
            main.gameObject.SetActive(false);

        // PlayerPrefab (cargar desde Resources)
        if (nm.NetworkConfig.PlayerPrefab == null)
        {
            var p = Resources.Load<GameObject>("PlayerNetwork");
            if (p == null) { Debug.LogError("[UI] Falta Resources/PlayerNetwork.prefab"); return false; }
            nm.NetworkConfig.PlayerPrefab = p;
            try { nm.AddNetworkPrefab(p); } catch { /* ok */ }
        }

        // Connection Approval para que el server cree el Player
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = OnApproval;

        return true;
    }

    static void OnApproval(NetworkManager.ConnectionApprovalRequest req,
                           NetworkManager.ConnectionApprovalResponse resp)
    {
        resp.Approved = true;
        resp.CreatePlayerObject = true;

        // Hash del prefab (compatible con distintas versiones de NGO)
        uint hash = 0;
        var pp = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        var no = pp != null ? pp.GetComponent<NetworkObject>() : null;
        if (no != null)
        {
            var t = no.GetType();
            var pGlobal = t.GetProperty("GlobalObjectIdHash", BindingFlags.Public | BindingFlags.Instance);
            var pPrefab = t.GetProperty("PrefabIdHash",       BindingFlags.Public | BindingFlags.Instance);
            var p = pGlobal ?? pPrefab;
            if (p != null)
            {
                var val = p.GetValue(no);
                if (val is uint u) hash = u;
                else if (val is int i) hash = unchecked((uint)i);
                else if (val is ulong ul) hash = unchecked((uint)ul);
                else if (val is long l) hash = unchecked((uint)l);
            }
        }
        resp.PlayerPrefabHash = hash;

        resp.Position = Vector3.zero;
        resp.Rotation = Quaternion.identity;
    }
}
