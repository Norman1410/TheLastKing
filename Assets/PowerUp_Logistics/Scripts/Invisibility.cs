using UnityEngine;

public class Invisibility : PowerUp
{
    // 'effectDuration' y 'renderers' YA NO son necesarios aquí.

    public override void Activate(GameObject player)
    {
        // 1. Intentamos añadir el poder al PowerManager.
        bool added = PowerManager.Instance.AddPower(PowerType.Invisibility, this.gameObject);

        if (added)
        {
            // 2. Si se añadió con éxito, destruimos el objeto de recogida.
            Debug.Log("Invisibilidad recogida. Añadida al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Slots de poder llenos.");
        }
    }

    // ELIMINAMOS la corrutina WaitForActivation(), SetVisible() y toda la lógica de renderizado.
}