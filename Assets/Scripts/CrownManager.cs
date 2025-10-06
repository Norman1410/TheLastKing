using UnityEngine;
using Unity.Netcode;

// Singleton NetworkBehaviour that stores crown ownership as a NetworkVariable.
public class CrownManager : NetworkBehaviour
{
    public static CrownManager Instance { get; private set; }

    // 0 = no owner, otherwise ClientId of owner
    public NetworkVariable<ulong> CrownOwner = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        // Por defecto hacemos persistente el CrownManager entre escenas
        DontDestroyOnLoad(gameObject);
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    // Server-side helper to set initial owner
    public void SetOwnerServer(ulong clientId)
    {
        if (!IsServer) return;
        CrownOwner.Value = clientId;
    }
}
