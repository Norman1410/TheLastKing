using UnityEngine;
using System.Collections;

public class TurboSprint : PowerUp
{
    public float speedMultiplier = 2f;   // Qué tanto aumenta la velocidad
    public float duration = 6f;          // Cuánto dura el poder

    private FirstPersonController playerController;
    private bool isCollected = false;

    public override void Activate(GameObject player)
    {
        playerController = player.GetComponent<FirstPersonController>();

        if (playerController != null)
        {
            isCollected = true;
            Debug.Log("TurboSprint recogido. Presiona R para activarlo.");
            gameObject.SetActive(false);

            // Le decimos al jugador que tiene este poder disponible
            playerController.StartCoroutine(WaitForActivation());
        }
    }

    private IEnumerator WaitForActivation()
    {
        // Espera hasta que el jugador presione R
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.R));

        Debug.Log("TurboSprint activado!");
        float originalSpeed = playerController.walkSpeed;
        playerController.walkSpeed *= speedMultiplier;

        // Espera el tiempo de duración del poder
        yield return new WaitForSeconds(duration);

        // Vuelve la velocidad a la normalidad
        playerController.walkSpeed = originalSpeed;
        Debug.Log("TurboSprint terminado.");

        // Destruye el objeto del poder
        Destroy(gameObject);
    }
}
