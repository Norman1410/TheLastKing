using UnityEngine;
using System.Collections;

public class SuperJump : PowerUp
{
    public float jumpMultiplier = 2f;

    public override void Activate(GameObject player)
    {
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.jumpHeight *= jumpMultiplier;
            player.GetComponent<MonoBehaviour>().StartCoroutine(ResetJump(pm));
        }
    }

    private IEnumerator ResetJump(PlayerMovement pm)
    {
        yield return new WaitForSeconds(duration);
        pm.jumpHeight /= jumpMultiplier;
    }
}
