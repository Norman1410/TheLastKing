using UnityEngine;
using System.Collections;

public class MegaSize : PowerUp
{
    public float sizeMultiplier = 3f; // cuánto crece el jugador
    public float effectDuration = 6f;         // duración del poder
    private bool isCollected = false;

    private Vector3 originalScale;

    public override void Activate(GameObject player)
    {
        // Guardamos la escala original del jugador
        originalScale = player.transform.localScale;

        if (!isCollected)
        {
            isCollected = true;
            Debug.Log("MegaSize recogido. Presiona R para activarlo.");

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

        Debug.Log("¡MegaSize activado!");

        // Aumentar tamaño
        player.transform.localScale = originalScale * sizeMultiplier;

        // Esperar la duración del efecto
        yield return new WaitForSeconds(effectDuration);

        // Restaurar tamaño original
        player.transform.localScale = originalScale;
        Debug.Log("¡MegaSize terminó!");

        // Destruir el objeto del poder
        Destroy(gameObject);
    }
}