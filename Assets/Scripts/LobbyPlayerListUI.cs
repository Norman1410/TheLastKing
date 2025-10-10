using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// SINGLE script to update player list in ALL lobby menus.
/// Works for LAN Host, LAN Ready, Online Host, Online Ready.
/// Optionally shows Join Code if Online Host.
/// </summary>
public class LobbyPlayerListUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerListText;
    
    [Header("Only for Online Host (optional)")]
    [SerializeField] private TMP_Text joinCodeText;

    private void Update()
    {
        if (LanLobbyState.Instance == null)
        {
            if (playerListText != null)
                playerListText.text = "Waiting for connection...";
            if (joinCodeText != null)
                joinCodeText.text = "Code: Generating...";
            return;
        }

        UpdatePlayerList();
        
        if (joinCodeText != null)
            UpdateJoinCode();
    }

    private void UpdatePlayerList()
    {
        if (playerListText == null) return;
        
        var players = LanLobbyState.Instance.Players;
        
        if (players.Count == 0)
        {
            playerListText.text = "No players connected";
            return;
        }

        string list = "";
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            string readyStatus = player.Ready ? "[Ready]" : "[Not Ready]";
            list += $"{player.Name} - {readyStatus}\n";
        }

        playerListText.text = list;
    }

    private void UpdateJoinCode()
    {
        // Only used in Online Host where joinCodeText is assigned
        // Code comes from RelayStartHelpers.LastJoinCode
        string code = RelayStartHelpers.LastJoinCode;
        
        if (string.IsNullOrEmpty(code))
        {
            joinCodeText.text = "Code: Generating...";
        }
        else
        {
            joinCodeText.text = "Code: " + code;
        }
    }
}
