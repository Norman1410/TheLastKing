using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class CrownGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int numberOfCrowns = 2;
    
    private List<PlayerRob> allPlayers = new List<PlayerRob>();
    private bool crownsAssigned = false;

    public static CrownGameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Suscribirse a eventos de spawn de jugadores
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        
        // Si ya hay jugadores spawneados (reconexión), buscarlos
        FindAllPlayers();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        Debug.Log($"Cliente {clientId} conectado. Esperando spawn de jugador...");
        
        // Esperar un frame para que el jugador se spawne
        StartCoroutine(CheckAndAssignCrownsDelayed());
    }

    System.Collections.IEnumerator CheckAndAssignCrownsDelayed()
    {
        yield return new WaitForSeconds(0.5f); // Dar tiempo al spawn
        
        FindAllPlayers();
        
        // Si todos los jugadores esperados están conectados, asignar coronas
        var lobbyState = LanLobbyState.Instance;
        if (lobbyState != null)
        {
            int expectedPlayers = lobbyState.Players.Count;
            if (allPlayers.Count >= expectedPlayers && !crownsAssigned)
            {
                AssignRandomCrowns();
            }
        }
        else
        {
            // Para Relay, asignar cuando haya suficientes jugadores
            if (allPlayers.Count >= 2 && !crownsAssigned)
            {
                AssignRandomCrowns();
            }
        }
    }

    void FindAllPlayers()
    {
        allPlayers.Clear();
        PlayerRob[] players = FindObjectsByType<PlayerRob>(FindObjectsSortMode.None);
        
        // Solo contar jugadores con NetworkObject spawneado
        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                allPlayers.Add(player);
            }
        }
        
        Debug.Log($"[Server] Encontrados {allPlayers.Count} jugadores spawneados");
    }

    void AssignRandomCrowns()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Solo el servidor puede asignar coronas!");
            return;
        }

        if (allPlayers.Count == 0)
        {
            Debug.LogWarning("No hay jugadores para asignar coronas!");
            return;
        }

        int crownsToAssign = Mathf.Min(numberOfCrowns, allPlayers.Count);
        List<PlayerRob> availablePlayers = new List<PlayerRob>(allPlayers);

        // Quitar todas las coronas
        foreach (PlayerRob player in allPlayers)
        {
            player.SetCrownDirect(false);
        }

        // Asignar coronas aleatorias
        for (int i = 0; i < crownsToAssign; i++)
        {
            int randomIndex = Random.Range(0, availablePlayers.Count);
            PlayerRob selectedPlayer = availablePlayers[randomIndex];

            selectedPlayer.SetCrownDirect(true);
            availablePlayers.RemoveAt(randomIndex);

            Debug.Log($"[Server] {selectedPlayer.gameObject.name} comienza con corona!");
        }

        crownsAssigned = true;
        Debug.Log($"[Server] Coronas asignadas: {crownsToAssign}/{allPlayers.Count} jugadores");
    }

    // Método público para forzar asignación (llamar desde host)
    public void ForceAssignCrowns()
    {
        if (!IsServer) return;
        
        crownsAssigned = false;
        FindAllPlayers();
        AssignRandomCrowns();
    }

    // Reiniciar juego
    public void RestartGame()
    {
        if (!IsServer) return;
        
        crownsAssigned = false;
        FindAllPlayers();
        AssignRandomCrowns();
    }

    // Estadísticas
    public int GetPlayersWithCrown()
    {
        int count = 0;
        foreach (PlayerRob player in allPlayers)
        {
            if (player.HasCrown())
                count++;
        }
        return count;
    }
}