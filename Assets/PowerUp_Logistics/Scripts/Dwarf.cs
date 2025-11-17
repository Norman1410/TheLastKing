using UnityEngine;

public class Dwarf : PowerUp
{
    // Las variables 'sizeSplit', 'effectDuration', 'originalScale' YA NO son necesarias aquí, 
    // pues la lógica del efecto se mueve a PowerManager.
    
    // No necesitamos más variables internas.

    public override void Activate(GameObject player)
    {
        // 1. Intentamos añadir el poder al PowerManager.
        // PowerType.MegaSize es el tipo de enum que usa PowerManager para este efecto.
        bool added = PowerManager.Instance.AddPower(PowerType.Dwarf, this.gameObject);

        if (added)
        {
            // 2. Si se añadió con éxito (había slot disponible), destruimos el objeto de recogida.
            Debug.Log("Dwarf recogido. Añadido al PowerManager.");
            Destroy(gameObject);
        }
        else
        {
            // Opcional: Si no se pudo añadir.
            Debug.Log("Slots de poder llenos.");
            // Si quieres que el objeto permanezca en el mundo si no hay slot, no hagas nada aquí.
        }
    }
    
    // ELIMINAMOS la corrutina WaitForActivation() y toda la lógica del efecto de tamaño.
}