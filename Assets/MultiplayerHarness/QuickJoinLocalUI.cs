#if TLK_QUICKJOIN
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
        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) Debug.LogError("[UI] No hay NetworkManager en la escena.");
        return nm;
    }

    bool PrepareConfig(NetworkManager nm)
    {
        if (nm == null) return false;
        if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();

        // Apaga Main Camera si no es de red
        var main = Camera.main;
        if (main != null && main.transform.GetComponentInParent<NetworkObject>() == null)
            main.gameObject.SetActive(false);

        // PlayerPrefab desde Resources (sirve para MultiplayerHarness/Resources)
        if (nm.NetworkConfig.PlayerPrefab == null)
        {
            var p = Resources.Load<GameObject>("PlayerNetwork");
            if (p == null) { Debug.LogError("[UI] Falta Resources/PlayerNetwork.prefab"); return false; }
            nm.NetworkConfig.PlayerPrefab = p;
        }
        // NO agregar nada a la lista de prefabs aquí.


        // Connection Approval: auto-create player (solo quick-join)
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = OnApproval;

        return true;
    }

    static bool AlreadyInList(NetworkManager nm, GameObject prefab)
    {
        foreach (var np in nm.NetworkConfig.NetworkPrefabs)
            if (np.Prefab == prefab) return true;
        return false;
    }

    static void OnApproval(NetworkManager.ConnectionApprovalRequest req,
                           NetworkManager.ConnectionApprovalResponse resp)
    {
        resp.Approved = true;
        resp.CreatePlayerObject = true;

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
                if      (val is uint  u ) hash = u;
                else if (val is int   i ) hash = unchecked((uint)i);
                else if (val is ulong ul) hash = unchecked((uint)ul);
                else if (val is long  l ) hash = unchecked((uint)l);
            }
        }
        resp.PlayerPrefabHash = hash;
        resp.Position = Vector3.zero;
        resp.Rotation = Quaternion.identity;
    }
}
#endif
