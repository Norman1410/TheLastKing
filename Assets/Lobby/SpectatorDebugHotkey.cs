using UnityEngine;
using Unity.Netcode;

public class SpectatorDebugHotkey : NetworkBehaviour
{
    void Update()
    {
        if (!IsOwner) return;
        if (Input.GetKeyDown(KeyCode.P)) // P = toggle a espectador para prueba
        {
            var pr = GetComponent<PlayerRob>();
            if (pr && IsServer) pr.EnterSpectatorServer();
            else if (pr) pr.EnterSpectatorServerRpc(); // cliente pide al server
        }
    }
}
