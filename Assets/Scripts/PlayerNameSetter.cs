using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Allows player to set their custom name from the main menu.
/// This name will be used in multiplayer lobbies.
/// </summary>
public class PlayerNameSetter : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button saveNameButton;
    [SerializeField] private TMP_Text currentNameDisplay;

    private void Start()
    {
        // Load current name and display it
        RefreshNameDisplay();
        
        // Set input field to current name
        if (nameInputField != null)
        {
            nameInputField.text = PlayerName.Get();
        }

        // Setup save button
        if (saveNameButton != null)
        {
            saveNameButton.onClick.AddListener(SavePlayerName);
        }
    }

    public void SavePlayerName()
    {
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
        {
            string newName = nameInputField.text.Trim();
            PlayerName.Set(newName);
            RefreshNameDisplay();
            Debug.Log($"Player name saved: {newName}");
        }
        else
        {
            Debug.LogWarning("Cannot save empty name");
        }
    }

    private void RefreshNameDisplay()
    {
        if (currentNameDisplay != null)
        {
            currentNameDisplay.text = $"Current Name: {PlayerName.Get()}";
        }
    }

    // Can be called from UI buttons
    public void OnNameChanged()
    {
        // Auto-save when input field changes (optional)
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
        {
            SavePlayerName();
        }
    }
}