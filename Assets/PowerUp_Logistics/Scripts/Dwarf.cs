using UnityEngine;
using System.Collections;

public class Dwarf : PowerUp
{
    public float sizeSplit = 0.3f; // cuanto se encoge el jugador
    public float effectDuration = 6f;         // duracion del poder
    private bool isCollected = false;

    private Vector3 originalScale;

    public override void Activate(GameObject player)
    {
        // Guardamos la escala original del jugador
        originalScale = player.transform.localScale;

        if (!isCollected)
        {
            isCollected = true;
            Debug.Log("Dwarf recogido. Presiona R para activarlo.");

            // Desactivar el objeto del poder
            gameObject.SetActive(false);

            // Esperar a que el jugador presione R
            player.GetComponent<MonoBehaviour>().StartCoroutine(WaitForActivation(player));
        }
    }

    private IEnumerator WaitForActivation(GameObject player)
    {
        // Espera a que el jugador presione R
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.R));

        Debug.Log("Dwarf activado!");

        // Reducir tamaño
        player.transform.localScale = originalScale * sizeSplit;

        // Esperar la duracion del efecto
        yield return new WaitForSeconds(effectDuration);

        // Restaurar tamaño original
        player.transform.localScale = originalScale;
        Debug.Log("Dwarf termino!");

        // Destruir el objeto del poder
        Destroy(gameObject);
    }
}