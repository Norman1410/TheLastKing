using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class CrownGameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] [Range(1, 100)] private int crownPercentage = 33;
    [SerializeField] private float delayBeforeAssign = 1.5f;
    
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
        int crownsToAssign = Mathf.Max(1, Mathf.RoundToInt(totalPlayers * (crownPercentage / 100f)));
        crownsToAssign = Mathf.Min(crownsToAssign, totalPlayers);

        Debug.Log($"[CrownGameManager] Asignando {crownsToAssign} coronas ({crownPercentage}%) entre {totalPlayers} jugadores");

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