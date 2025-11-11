using UnityEngine;
using System.Collections;

public class WallAppearsController : MonoBehaviour
{
    // Variables for setup in the Inspector
    public GameObject wallObject;         // Assign the Wall_Mesh object here
    public float activeTime = 2.0f;        // Time the wall is visible
    public float inactiveTime = 3.0f;      // Time the wall is hidden
    
    void Start()
    {
        // Start the continuous cycle
        StartCoroutine(WallCycle());
    }

    IEnumerator WallCycle()
    {
        while (true) // Loop indefinitely
        {
            // 1. Wall is HIDDEN
            wallObject.SetActive(false);
            yield return new WaitForSeconds(inactiveTime);

            // 2. Wall is VISIBLE
            wallObject.SetActive(true);
            yield return new WaitForSeconds(activeTime);
        }
    }
}