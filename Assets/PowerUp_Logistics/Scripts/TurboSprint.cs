using UnityEngine;

public class TurboSprint : PowerUp
{
    public float speedMultiplier = 2f;

    public override void Activate(GameObject player)
    {
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.speed *= speedMultiplier;
            // Volver a la velocidad normal después de duration segundos
            StartCoroutine(ResetSpeed(pm));
        }
    }

    private System.Collections.IEnumerator ResetSpeed(PlayerMovement pm)
    {
        yield return new WaitForSeconds(duration);
        pm.speed /= speedMultiplier;
    }
}
