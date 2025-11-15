using UnityEngine;
using Unity.Netcode;

public class CrownPickup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            PlayerRob pr = other.GetComponent<PlayerRob>();
            if (pr != null && !pr.HasCrown())
            {
                pr.SetCrownDirect(true);
                NetworkObject.Despawn();
            }
        }
    }
}
