using UnityEngine;
using System.Collections;

public class SuperJump : PowerUp 
{
    
    public override void Activate(GameObject player)
    {
        // 1. Llama al PowerManager para registrar el poder.
        // El PowerManager usa el tipo de componente o el prefab para saber qué icono y efecto aplicar.
        bool added = PowerManager.Instance.AddPower(PowerType.JumpHigh, this.gameObject); 

        if (added)
        {
            // 2. Si se agregó con éxito (había slot), destruye el objeto.
            Destroy(gameObject);
        }
        else
        {
            // 3. Si no hay slots, podría rebotar o dar un mensaje.
            Debug.Log("Slots llenos. No se recoge el poder.");
        }
    }
    
    // 4. ELIMINA toda la corrutina WaitForActivation() y la lógica de activación.
    // Esa lógica ahora está centralizada en PowerManager.HandlePowerEffect(PowerType.JumpHigh)
}
