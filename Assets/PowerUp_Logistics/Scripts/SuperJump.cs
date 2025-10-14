using UnityEngine;
using System.Collections;

public class SuperJump : PowerUp
{
    public float jumpMultiplier = 4f;   // Qué tanto aumenta el salto
    public float effectDuration = 6f;         // Cuánto dura el poder

    private FirstPersonController playerController;
    private bool isCollected = false;

    public override void Activate(GameObject player)
    {
        playerController = player.GetComponent<FirstPersonController>();

        if (playerController != null)
        {
            isCollected = true;
            Debug.Log("Super Jump recogido. Presiona R para activarlo.");
            gameObject.SetActive(false);

            // Corrutina que espera a que el jugador presione R
            playerController.StartCoroutine(WaitForActivation());
        }
    }

    private IEnumerator WaitForActivation()
    {
        // Espera hasta que presione la tecla R
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.R));

        Debug.Log("Super Jump activado!");
        float originalJump = playerController.jumpHeight;

        // Aumenta la altura de salto
        playerController.jumpHeight *= jumpMultiplier;

        // Espera el tiempo de duración
        yield return new WaitForSeconds(effectDuration);

        // Restaura el salto original
        playerController.jumpHeight = originalJump;
        Debug.Log("Super Jump terminado.");

        // Destruye el objeto después de usarlo
        Destroy(gameObject);
    }
}
