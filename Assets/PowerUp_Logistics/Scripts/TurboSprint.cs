using UnityEngine;

public class TurboSprint : PowerUp
{
    // 'speedMultiplier', 'effectDuration', 'playerController' YA NO son necesarios aquí.

    public override void Activate(GameObject player)
    {
        // 1. Intentamos añadir el poder al PowerManager.
        bool added = PowerManager.Instance.AddPower(PowerType.Boost, this.gameObject);

        if (added)
        {
            // 2. Si se añadió con éxito, destruimos el objeto de recogida.
            Debug.Log("TurboSprint recogido. Añadido al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Slots de poder llenos.");
        }
    }

    // ELIMINAMOS la corrutina WaitForActivation() y toda la lógica de velocidad.
}