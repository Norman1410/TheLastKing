using UnityEngine;
using System.Collections;

public class WallAppearsController : MonoBehaviour
{
    [Header("Wall Components")]
    public GameObject wallMesh;        // The visible mesh
    public BoxCollider wallCollider;   // The collider that blocks the player

    [Header("Wall Timing")]
    public float activeTime = 8f;      // Time the wall stays active
    public float inactiveTime = 3f;    // Time the wall disappears

    [Header("Crown Drop Settings")]
    [Tooltip("Direction the crown will be launched when the wall is hit")]
    public Vector3 dropDirection = new Vector3(0, -1, 0);
    public float dropForce = 3f;

    private bool isActive = true;
    private bool isCycling = false;

    private void Awake()
    {
        if (wallCollider == null)
            wallCollider = GetComponent<BoxCollider>();

        if (wallMesh == null)
            wallMesh = gameObject;
    }

    private void Start()
    {
        ActivateWall();
        Debug.Log($"[WallTrap] Wall is ACTIVE (Layer = {LayerMask.LayerToName(gameObject.layer)})");
    }

    /// <summary>
    /// Called by the player when they bump into this wall.
    /// The crown drop is done on the player; here we just start the wall cycle.
    /// </summary>
    public void OnPlayerHit()
    {
        if (!isActive || isCycling) return;
        StartCoroutine(WallCycle());
    }

    // --- WALL ACTIVE/INACTIVE CYCLE ---
    private IEnumerator WallCycle()
    {
        isCycling = true;

        DeactivateWall();
        yield return new WaitForSeconds(inactiveTime);

        ActivateWall();
        yield return new WaitForSeconds(activeTime);

        isCycling = false;
    }

    private void ActivateWall()
    {
        isActive = true;

        if (wallMesh != null)
            wallMesh.SetActive(true);

        if (wallCollider != null)
        {
            wallCollider.enabled = true;
            wallCollider.isTrigger = false;  // the wall should block the player
        }

        Debug.Log("[WallTrap] Wall is ACTIVE");
    }

    private void DeactivateWall()
    {
        isActive = false;

        if (wallMesh != null)
            wallMesh.SetActive(false);

        if (wallCollider != null)
            wallCollider.enabled = false;

        Debug.Log("[WallTrap] Wall is INACTIVE");
    }
}
