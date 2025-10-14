using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// Small NetworkBehaviour that stores a networked display name for the Player object.
/// The owner writes its name on spawn and other peers read it. This makes player
/// nicknames reliable both for LAN (Netcode lobby) and Relay (Unity Lobbies) flows.
/// </summary>
public class PlayerNetworkDisplayName : NetworkBehaviour
{
    // Owner may write this value; everyone can read it.
    // Server will be authoritative for setting display names when clients register.
    public NetworkVariable<FixedString64Bytes> DisplayName =
        new NetworkVariable<FixedString64Bytes>(default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Server is authoritative for DisplayName; do not let owner write it here.
    }
}
