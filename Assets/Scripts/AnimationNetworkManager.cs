using Unity.Netcode;
using UnityEngine;

public class AnimationNetworkManager : NetworkBehaviour
{
    public static AnimationNetworkManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    /// <summary>
    /// El propietario llama a este ServerRpc para enviar su estado de animación al servidor.
    /// El servidor lo retransmitirá a todos los clientes mediante un ClientRpc.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SubmitAnimationStateServerRpc(float speed, float forward, float strafe, bool isJumping, bool isRunning, bool isGrounded, ServerRpcParams rpcParams = default)
    {
        ulong ownerClientId = rpcParams.Receive.SenderClientId;

        // Server-side validation: ensure the sender is a connected client and has a PlayerObject
        if (!IsServer)
        {
            Debug.LogWarning($"SubmitAnimationStateServerRpc: called on non-server instance by {ownerClientId}");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("SubmitAnimationStateServerRpc: NetworkManager.Singleton is null");
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client))
        {
            Debug.LogWarning($"SubmitAnimationStateServerRpc: sender client {ownerClientId} not found in ConnectedClients");
            return;
        }

        if (client.PlayerObject == null)
        {
            Debug.LogWarning($"SubmitAnimationStateServerRpc: sender client {ownerClientId} has no PlayerObject assigned yet");
            return;
        }

        // Broadcast to all clients the animation state for this owner (server has validated the sender)
        Debug.Log($"SubmitAnimationStateServerRpc: broadcasting animation state from client {ownerClientId} (speed={speed}, forward={forward}, strafe={strafe})");
        BroadcastAnimationClientRpc(ownerClientId, speed, forward, strafe, isJumping, isRunning, isGrounded);
    }

    /// <summary>
    /// Difunde el estado de animación a los clientes. Los clientes lo aplicarán a la instancia de jugador correspondiente.
    /// </summary>
    [ClientRpc]
    private void BroadcastAnimationClientRpc(ulong ownerClientId, float speed, float forward, float strafe, bool isJumping, bool isRunning, bool isGrounded, ClientRpcParams clientRpcParams = default)
    {
        // Find the local player instance that corresponds to ownerClientId and apply parameters
        var players = GameObject.FindObjectsOfType<FirstPersonController>(true);
        foreach (var p in players)
        {
            if (p == null) continue;

            // Try to find a NetworkObject on the player GameObject or its parents
            var netObj = p.GetComponentInParent<Unity.Netcode.NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == ownerClientId)
            {
                p.ApplyRemoteAnimationState(speed, forward, strafe, isJumping, isRunning, isGrounded);
                break;
            }
        }
    }
}
