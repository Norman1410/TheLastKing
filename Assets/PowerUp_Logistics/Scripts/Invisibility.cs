using UnityEngine;
using System.Collections;

public class Invisibility : PowerUp
{
    public float effectDuration = 6f; // duración del efecto
    private bool isCollected = false;

    private Renderer[] renderers; // para ocultar todas las partes visibles del jugador

    public override void Activate(GameObject player)
    {
        // Guardamos todos los renderers del jugador (por si tiene varios meshes)
        renderers = player.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            isCollected = true;
            Debug.Log("Invisibilidad recogida. Presiona R para activarla.");
            gameObject.SetActive(false);

            // Ejecuta la corrutina desde el jugador
            player.GetComponent<MonoBehaviour>().StartCoroutine(WaitForActivation(player));
        }
        else
        {
            Debug.LogWarning("No se encontraron renderers en el jugador para aplicar invisibilidad.");
        }
    }

    private IEnumerator WaitForActivation(GameObject player)
    {
        // Espera a que el jugador presione R
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.R));

        Debug.Log("¡Invisibilidad activada!");
        SetVisible(false); // oculta al jugador

        yield return new WaitForSeconds(effectDuration);

        SetVisible(true); // vuelve visible al jugador
        Debug.Log("¡Invisibilidad terminada!");

        Destroy(gameObject);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
    }
}
