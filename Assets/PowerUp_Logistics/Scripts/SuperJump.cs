using UnityEngine;
using System.Collections;

public class SuperJump : PowerUp 
{
    
    public override void Activate(GameObject player)
    {
        // El PowerManager usa el tipo de componente o el prefab para saber qué icono y efecto aplicar.
        bool added = PowerManager.Instance.AddPower(PowerType.JumpHigh, this.gameObject); 

        if (added)
        {
            // Si se agregó con éxito (había slot), destruye el objeto.
            Destroy(gameObject);
        }
        else
        {
            // Si no hay slots, podría rebotar o dar un mensaje.
            Debug.Log("Slots llenos. No se recoge el poder.");
        }
    }
}
