using UnityEngine;
using System.Collections.Generic;

public class CrownGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private int numberOfCrowns = 2; // Cantidad de jugadores que empiezan con corona
    [SerializeField] private List<PlayerRob> allPlayers = new List<PlayerRob>();
    
    [Header("Auto Find Players")]
    [SerializeField] private bool autoFindPlayers = true;
    
    void Start()
    {
        if (autoFindPlayers)
        {
            FindAllPlayers();
        }
        
        AssignRandomCrowns();
    }
    
    void FindAllPlayers()
    {
        allPlayers.Clear();
         PlayerRob[] players = FindObjectsByType<PlayerRob>(FindObjectsSortMode.None);
        allPlayers.AddRange(players);
        
        Debug.Log($"Se encontraron {allPlayers.Count} jugadores");
    }
        
    void AssignRandomCrowns()
    {
        if (allPlayers.Count == 0)
        {
            Debug.LogWarning("No hay jugadores para asignar coronas!");
            return;
        }
        
        // Asegurarse de que no asignemos más coronas que jugadores
        int crownsToAssign = Mathf.Min(numberOfCrowns, allPlayers.Count);
        
        // Crear una lista temporal para selección aleatoria
        List<PlayerRob> availablePlayers = new List<PlayerRob>(allPlayers);
        
        // Primero, quitar todas las coronas
        foreach (PlayerRob player in allPlayers)
        {
            player.SetCrown(false);
        }
        
        // Asignar coronas aleatorias
        for (int i = 0; i < crownsToAssign; i++)
        {
            int randomIndex = Random.Range(0, availablePlayers.Count);
            PlayerRob selectedPlayer = availablePlayers[randomIndex];
            
            selectedPlayer.SetCrown(true);
            availablePlayers.RemoveAt(randomIndex);
            
            Debug.Log($"{selectedPlayer.gameObject.name} comienza con corona!");
        }
    }
    
    // Método público para reiniciar el juego
    public void RestartGame()
    {
        if (autoFindPlayers)
        {
            FindAllPlayers();
        }
        AssignRandomCrowns();
    }
    
    // Obtener estadísticas del juego
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