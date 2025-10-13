using Unity.Netcode;
using UnityEngine;

public class OwnerOnlyEnabler : NetworkBehaviour
{
    [Header("Scripts (MonoBehaviours) que solo usa el Owner")]
    [SerializeField] Behaviour[] ownerBehaviours; // ej: FirstPersonController, tu HUD local, etc.

    [Header("Colliders/CharacterController que solo usa el Owner")]
    [SerializeField] Collider[] ownerColliders;   // aquí puedes arrastrar CharacterController

    [Header("GameObjects solo del Owner (raíz de cámara, HUD local, etc.)")]
    [SerializeField] GameObject[] ownerGameObjects;

    public override void OnNetworkSpawn()      => Apply(IsOwner);
    public override void OnGainedOwnership()   => Apply(true);
    public override void OnLostOwnership()     => Apply(false);

    void Apply(bool isOwner)
    {
        if (ownerBehaviours != null)
            foreach (var b in ownerBehaviours) if (b) b.enabled = isOwner;

        if (ownerColliders != null)
            foreach (var c in ownerColliders) if (c) c.enabled = isOwner;

        if (ownerGameObjects != null)
            foreach (var go in ownerGameObjects) if (go) go.SetActive(isOwner);

        // Garantía: cámaras y AudioListener solo en el dueño
        foreach (var cam in GetComponentsInChildren<Camera>(true)) cam.enabled = isOwner;
        foreach (var al in GetComponentsInChildren<AudioListener>(true)) al.enabled = isOwner;
    }
}
