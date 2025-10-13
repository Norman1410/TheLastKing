using UnityEngine;
using Unity.Netcode;

public class MenuCameraSwitcher : MonoBehaviour
{
    void Update()
    {
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening) return;
        NetworkManager.Singleton.StartHost();
    }
}
