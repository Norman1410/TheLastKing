using Unity.Netcode;
using UnityEngine;

public class OwnerDebugHUD : NetworkBehaviour
{
    void OnGUI()
    {
        var r = new Rect(10, 80, 400, 100);
        GUILayout.BeginArea(r, GUI.skin.box);
        GUILayout.Label($"IsOwner: {IsOwner}  |  IsLocalPlayer: {IsLocalPlayer}  |  ClientId: {NetworkManager.Singleton?.LocalClientId}");
        if (IsOwner)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            GUILayout.Label($"Input H: {h:F2}  V: {v:F2}");
        }
        GUILayout.EndArea();
    }
}
