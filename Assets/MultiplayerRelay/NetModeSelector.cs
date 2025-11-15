using UnityEngine;

// Enum con los modos posibles
public enum NetMode
{
    None,
    LAN,
    Relay
}

// Runtime global para guardar el modo actual
public static class NetRuntime
{
    // Empezamos en None, así no se dibuja LAN ni Relay hasta que elijas.
    public static NetMode Mode = NetMode.None;
}

// Este componente ahora NO dibuja nada.
// Solo existe para mantener compatibilidad si algún GameObject aún lo tiene.
public class NetModeSelector : MonoBehaviour
{
    private void Awake()
    {
        // Ya no tocamos NetRuntime.Mode aquí.
        // El modo se decide con tu nueva UI (NetModeSelectorUI).
    }

    private void OnGUI()
    {
        // Antes aquí estaba la ventana fea "Seleccionar modo de red".
        // Lo dejamos vacío para que no pinte nada.
    }
}
