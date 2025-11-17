using UnityEngine;

public class Dwarf : PowerUp
{

    public override void Activate(GameObject player)
    {
        // PowerType.Dwarf es el tipo de enum que usa PowerManager para este efecto.
        bool added = PowerManager.Instance.AddPower(PowerType.Dwarf, this.gameObject);

        if (added)
        {
            // Si se añadió con éxito (había slot disponible), destruimos el objeto de recogida.
            Debug.Log("Dwarf recogido. Añadido al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            // Si no se pudo añadir.
            Debug.Log("Slots de poder llenos.");
        }
    }
}