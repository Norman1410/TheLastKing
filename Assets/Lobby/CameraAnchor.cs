using UnityEngine;

public class CameraAnchor : MonoBehaviour
{
    [SerializeField] private Transform spectatorPivot; // child under SpectatorController
    [SerializeField] private Transform playerPivot;    // your existing player camera holder

    private Camera _main;

    void Start()
    {
        _main = Camera.main;
        if (_main == null)
        {
            var camObj = new GameObject("MainCamera");
            _main = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
    }

    public void SetActiveLocal(bool spectator)
    {
        if (_main == null) return;
        if (spectator) ParentAndReset(_main.transform, spectatorPivot);
        else ParentAndReset(_main.transform, playerPivot);
    }

    private void ParentAndReset(Transform t, Transform parent)
    {
        if (parent == null) return;
        t.SetParent(parent);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
    }
}
