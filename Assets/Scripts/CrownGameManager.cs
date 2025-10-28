using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class CrownGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] [Range(0, 100)] private int crownPercentage = 33; // kept for compatibility but not used when using half rule
    [SerializeField] private float delayBeforeAssign = 1.5f;
    
    private List<PlayerRob> allPlayers = new List<PlayerRob>();
    private bool crownsAssigned = false;

    // Public API: assign crowns to a specific list of players (used for round-to-round assignment)
    public void AssignCrownsToPlayers(List<PlayerRob> players)
    {
        if (!IsServer || players == null || players.Count == 0) return;

        int totalPlayers = players.Count;
        int crownsToAssign = Mathf.Max(1, totalPlayers / 2);
        crownsToAssign = Mathf.Clamp(crownsToAssign, 1, totalPlayers);

        Debug.Log($"[CrownGameManager] AssignCrownsToPlayers: assigning {crownsToAssign} crowns among {totalPlayers} players");

        // Clear crowns first
        foreach (var p in players)
        {
            p.SetCrownDirect(false);
        }

        // Shuffle list
        List<PlayerRob> availablePlayers = new List<PlayerRob>(players);
        for (int i = availablePlayers.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            PlayerRob temp = availablePlayers[i];
            availablePlayers[i] = availablePlayers[randomIndex];
            availablePlayers[randomIndex] = temp;
        }

        for (int i = 0; i < crownsToAssign; i++)
        {
            availablePlayers[i].SetCrownDirect(true);
            Debug.Log($"[CrownGameManager] Crown assigned to player index {i + 1}");
        }
    }

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

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        StartCoroutine(InitialCheckDelayed());
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    System.Collections.IEnumerator InitialCheckDelayed()
    {
        yield return new WaitForSeconds(delayBeforeAssign);
        
        var lanLobby = LanLobbyState.Instance;
        if (lanLobby != null)
        {
            while (!lanLobby.GameStarted.Value)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        FindAllPlayers();
        TryAssignCrowns();
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(CheckAndAssignCrownsDelayed());
    }

    System.Collections.IEnumerator CheckAndAssignCrownsDelayed()
    {
        yield return new WaitForSeconds(delayBeforeAssign);
        FindAllPlayers();
        TryAssignCrowns();
    }

    void FindAllPlayers()
    {
        allPlayers.Clear();
        PlayerRob[] players = FindObjectsByType<PlayerRob>(FindObjectsSortMode.None);
        
        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                allPlayers.Add(player);
            }
        }
        
        Debug.Log($"[CrownGameManager] Jugadores encontrados: {allPlayers.Count}");
    }

    void TryAssignCrowns()
    {
        if (!IsServer || crownsAssigned) return;

        int expectedPlayers = GetExpectedPlayerCount();
        
        Debug.Log($"[CrownGameManager] Jugadores: {allPlayers.Count}/{expectedPlayers}");

        if (allPlayers.Count >= expectedPlayers && expectedPlayers > 0)
        {
            AssignCrownsByPercentage();
        }
    }

    int GetExpectedPlayerCount()
    {
        var lanLobby = LanLobbyState.Instance;
        if (lanLobby != null && lanLobby.Players.Count > 0)
        {
            return lanLobby.Players.Count;
        }

        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.ConnectedClientsIds.Count;
        }

        return 0;
    }

    void AssignCrownsByPercentage()
    {
        if (!IsServer || allPlayers.Count == 0) return;

        int totalPlayers = allPlayers.Count;
        // New rule: assign half of the players as crowned (floor). If there are 0 after division, ensure at least 1.
        int crownsToAssign = Mathf.Max(1, totalPlayers / 2);
        crownsToAssign = Mathf.Clamp(crownsToAssign, 1, totalPlayers);

        Debug.Log($"[CrownGameManager] Asignando {crownsToAssign} coronas (mitad de {totalPlayers}) entre {totalPlayers} jugadores");

        foreach (PlayerRob player in allPlayers)
        {
            player.SetCrownDirect(false);
        }

        List<PlayerRob> availablePlayers = new List<PlayerRob>(allPlayers);
        
        for (int i = availablePlayers.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            PlayerRob temp = availablePlayers[i];
            availablePlayers[i] = availablePlayers[randomIndex];
            availablePlayers[randomIndex] = temp;
        }

        for (int i = 0; i < crownsToAssign; i++)
        {
            availablePlayers[i].SetCrownDirect(true);
            Debug.Log($"[CrownGameManager] Corona asignada a jugador {i + 1}");
        }

        crownsAssigned = true;
    }

    public void ForceReassignCrowns()
    {
        if (!IsServer) return;
        crownsAssigned = false;
        FindAllPlayers();
        AssignCrownsByPercentage();
    }
}