using UnityEngine;
using System.Collections;

public class PowerUpManager : MonoBehaviour
{
    public Transform[] spawnPoints;        // Los puntos donde pueden aparecer
    public GameObject[] powerUpPrefabs;    // Array de prefabs de todos los tipos de power-ups
    public float respawnTime = 10f;        // Tiempo antes de reaparecer un power-up

    void Start()
    {
        SpawnAll();
    }

    void SpawnAll()
    {
        foreach (var point in spawnPoints)
        {
            SpawnAt(point);
        }
    }

    void SpawnAt(Transform point)
    {
        // Selecciona un power-up aleatorio del array
        int index = Random.Range(0, powerUpPrefabs.Length);
        GameObject p = Instantiate(powerUpPrefabs[index], point.position, point.rotation);

        // Suscribirse al evento onPicked del prefab
        PowerUp powerUpScript = p.GetComponent<PowerUp>();
        if (powerUpScript != null)
        {
            powerUpScript.onPicked += () => StartCoroutine(ReSpawn(point));
        }
    }

    IEnumerator ReSpawn(Transform point)
    {
        yield return new WaitForSeconds(respawnTime);
        SpawnAt(point);
    }
}
