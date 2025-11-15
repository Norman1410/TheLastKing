using UnityEngine;

public class CrownWallTrap : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        PlayerRob pr = other.gameObject.GetComponent<PlayerRob>();
        if (pr != null && pr.HasCrown())
        {
            pr.DropCrownWithAnimation("wall");
        }
    }
}
