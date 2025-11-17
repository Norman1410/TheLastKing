using UnityEngine;

public class TurboSprint : PowerUp
{
    public override void Activate(GameObject player)
    {
        // Intentamos añadir el poder al PowerManager.
        bool added = PowerManager.Instance.AddPower(PowerType.Boost, this.gameObject);

        if (added)
        {
            // Si se añadió con éxito, destruimos el objeto de recogida.
            Debug.Log("TurboSprint recogido. Añadido al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Slots de poder llenos.");
        }
    }
}