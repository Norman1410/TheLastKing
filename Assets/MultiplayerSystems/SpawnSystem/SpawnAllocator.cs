using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement; // <--- NUEVO

public class SpawnAllocator : NetworkBehaviour
{
    private NetworkSpawnPoints _spawns;

    // Quién usa qué índice
    private readonly Dictionary<ulong, int> _assigned = new();
    private readonly HashSet<int> _occupied = new();

    [Tooltip("Si no hay puntos libres, usa el índice 0 como fallback.")]
    public bool fallbackToZero = true;

    // =========================
    //  AWAKE + manejo de escenas
    // =========================
    private void Awake()
    {
        Debug.Log($"[SpawnAllocator] Awake en escena '{SceneManager.GetActiveScene().name}'");

        FindSpawnPoints();

        // Por si este mismo GO vive entre escenas (DontDestroyOnLoad)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsServer) return;

        Debug.Log($"[SpawnAllocator] Escena cargada: {scene.name}, recargando spawn points...");
        FindSpawnPoints();
    }

    private void FindSpawnPoints()
    {
        // Si hubiera más de uno, FindAnyObjectByType agarra uno cualquiera,
        // pero nos interesa ver QUIÉN fue para depurar.
        _spawns = FindAnyObjectByType<NetworkSpawnPoints>();

        if (_spawns == null)
        {
            Debug.LogError("[SpawnAllocator] NO se encontró NetworkSpawnPoints en la escena actual.");
        }
        else
        {
            Debug.Log($"[SpawnAllocator] Encontrado NetworkSpawnPoints en escena '{_spawns.gameObject.scene.name}' con {_spawns.Count} puntos.");
        }
    }

    // =========================
    //  Netcode lifecycle
    // =========================
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        Debug.Log("[SpawnAllocator] OnNetworkSpawn en SERVER, registrando callbacks.");

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // Por si el Host ya está conectado antes de que este componente se inicialice
        foreach (var kv in NetworkManager.ConnectedClients)
        {
            Debug.Log($"[SpawnAllocator] EnsureSpawn inicial para cliente {kv.Key}");
            EnsureSpawn(kv.Key);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        Debug.Log("[SpawnAllocator] OnNetworkDespawn en SERVER, removiendo callbacks.");
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // =========================
    //  Callbacks de clientes
    // =========================
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SpawnAllocator] OnClientConnected para {clientId}");
        EnsureSpawn(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SpawnAllocator] OnClientDisconnected para {clientId}");

        if (_assigned.TryGetValue(clientId, out int idx))
        {
            _assigned.Remove(clientId);
            _occupied.Remove(idx);
            Debug.Log($"[SpawnAllocator] Liberando índice {idx} de cliente {clientId}");
        }
    }

    // =========================
    //  Asignación de spawn
    // =========================
    private void EnsureSpawn(ulong clientId)
    {
        if (_spawns == null || _spawns.Count == 0)
        {
            Debug.LogWarning("[SpawnAllocator] No hay spawn points configurados.");
            return;
        }

        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogWarning($"[SpawnAllocator] ClientId {clientId} no está en ConnectedClients.");
            return;
        }

        var playerObj = client.PlayerObject;
        if (playerObj == null || !playerObj.IsSpawned)
        {
            // A veces Netcode aún no ha creado el PlayerObject cuando llega este callback
            Debug.Log($"[SpawnAllocator] PlayerObject de {clientId} aún no está listo, esperando...");
            StartCoroutine(WaitForPlayerObjectThenAssign(clientId));
            return;
        }

        AssignToPlayer(clientId, playerObj);
    }

    private IEnumerator WaitForPlayerObjectThenAssign(ulong clientId)
    {
        float timeout = 3f;
        while (timeout > 0f)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var po = client.PlayerObject;
                if (po != null && po.IsSpawned)
                {
                    Debug.Log($"[SpawnAllocator] PlayerObject de {clientId} listo tras espera. Asignando spawn.");
                    AssignToPlayer(clientId, po);
                    yield break;
                }
            }
            timeout -= Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning($"[SpawnAllocator] Timeout esperando PlayerObject de {clientId}");
    }

    private void AssignToPlayer(ulong clientId, NetworkObject playerObj)
    {
        // Ya asignado
        if (_assigned.ContainsKey(clientId))
        {
            Debug.Log($"[SpawnAllocator] Cliente {clientId} ya tiene spawn asignado (índice {_assigned[clientId]}).");
            return;
        }

        int index = FindFreeIndex();
        if (index < 0)
        {
            if (!fallbackToZero)
            {
                Debug.LogWarning($"[SpawnAllocator] No hay puntos libres y fallbackToZero = false. Cliente {clientId} se queda donde está.");
                return;
            }
            index = 0;
            Debug.LogWarning($"[SpawnAllocator] No hay puntos libres, usando índice 0 para cliente {clientId}");
        }

        _occupied.Add(index);
        _assigned[clientId] = index;

        Vector3 pos = _spawns.GetPoint(index);
        Quaternion rot = _spawns.GetRotation(index);

        Debug.Log($"[SpawnAllocator] ASIGNANDO spawn al cliente {clientId} en índice {index}, pos {pos}, rot {rot.eulerAngles}");

        TeleportPlayer(playerObj, pos, rot);
    }

    private int FindFreeIndex()
    {
        for (int i = 0; i < _spawns.Count; i++)
        {
            if (_occupied.Contains(i)) continue;
            if (_spawns.IsFree(i))
            {
                Debug.Log($"[SpawnAllocator] Índice libre encontrado: {i}");
                return i;
            }
        }
        Debug.Log("[SpawnAllocator] No se encontraron índices libres.");
        return -1;
    }

    private static void TeleportPlayer(NetworkObject playerObj, Vector3 pos, Quaternion rot)
    {
        var go = playerObj.gameObject;

        Debug.Log($"[SpawnAllocator] Teleportando {go.name} a {pos} (intentando via PlayerSpawnHandler)");

        // Intentar usar el handler del jugador (RPC hacia el dueño)
        // TODO :
        // var handler = go.GetComponent<PlayerSpawnHandler>();
        //if (handler != null)
        //{
        //    Debug.Log($"[SpawnAllocator] Encontrado PlayerSpawnHandler en {go.name}. Llamando TeleportClientRpc...");
        //    handler.TeleportClientRpc(pos, rot);
        //    return;
        //}

        Debug.LogWarning($"[SpawnAllocator] NO hay PlayerSpawnHandler en {go.name}. Usando fallback en servidor.");

        // Fallback por si algún día el prefab no tiene PlayerSpawnHandler
        var cc = go.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            go.transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
        }
        else
        {
            go.transform.SetPositionAndRotation(pos, rot);
        }

        Debug.Log($"[SpawnAllocator] Fallback: {go.name} colocado en {go.transform.position} desde el servidor.");
    }


}
