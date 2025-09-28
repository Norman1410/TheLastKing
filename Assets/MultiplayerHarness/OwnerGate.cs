using Unity.Netcode;
using UnityEngine;

public class OwnerGate : NetworkBehaviour
{
    [SerializeField] MonoBehaviour[] ownerOnlyBehaviours; // ej: FirstPersonController
    [SerializeField] GameObject[]   ownerOnlyObjects;    // ej: Camera

    public override void OnNetworkSpawn()
    {
        Apply(IsOwner);
    }

    public override void OnGainedOwnership() { Apply(true); }
    public override void OnLostOwnership()   { Apply(false); }

    void Apply(bool isOwner)
    {
        if (ownerOnlyBehaviours != null)
            foreach (var b in ownerOnlyBehaviours) if (b) b.enabled = isOwner;

        if (ownerOnlyObjects != null)
            foreach (var go in ownerOnlyObjects) if (go) go.SetActive(isOwner);

        if (isOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
