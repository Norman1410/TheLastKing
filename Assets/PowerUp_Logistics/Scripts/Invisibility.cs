using UnityEngine;

public class Invisibility : PowerUp
{
    public override void Activate(GameObject player)
    {
        // Intentamos añadir el poder al PowerManager.
        bool added = PowerManager.Instance.AddPower(PowerType.Invisibility, this.gameObject);

        if (added)
        {
            // Si se añadió con éxito, destruimos el objeto de recogida.
            Debug.Log("Invisibilidad recogida. Añadida al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Slots de poder llenos.");
        }
    }
}