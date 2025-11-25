using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnHandler : NetworkBehaviour
{
    [ClientRpc]
    public void TeleportClientRpc(Vector3 pos, Quaternion rot)
    {
        Debug.Log($"[PlayerSpawnHandler] TeleportClientRpc recibido en cliente {OwnerClientId}. IsOwner={IsOwner}. Pos objetivo={pos}");

        // Solo el dueño se mueve a sí mismo
        if (!IsOwner)
        {
            Debug.Log($"[PlayerSpawnHandler] Ignorando teleport en cliente {OwnerClientId} porque no es el dueño.");
            return;
        }

        var go = gameObject;
        var cc = go.GetComponent<CharacterController>();

        if (cc != null)
        {
            cc.enabled = false;
            go.transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
        }
        else
        {
            go.transform.SetPositionAndRotation(pos, rot);
        }

        Debug.Log($"[PlayerSpawnHandler] Cliente {OwnerClientId} colocado en {go.transform.position}");
    }
}
