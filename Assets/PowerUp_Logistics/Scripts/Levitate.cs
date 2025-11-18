using UnityEngine;

public class Levitate : PowerUp
{
    public override void Activate(GameObject player)
    {
        // Intentamos añadir el poder al PowerManager, pasando el PowerType y el objeto pickup.
        bool added = PowerManager.Instance.AddPower(PowerType.Levitate, this.gameObject);

        if (added)
        {
            Debug.Log("Poder de levitación recogido. Añadido al PowerManager.");
            // Si se añadió, el objeto del pickup se destruye.
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Slots de poder llenos. No se puede recoger Levitación.");
        }
    }
    
}