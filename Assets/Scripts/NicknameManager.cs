using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NicknameManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nicknameInputField; // Input field in the menu
    public Button saveButton; // Optional save button (or can save automatically)
    
    private const string NICKNAME_KEY = "PlayerNickname"; // Key for saving in PlayerPrefs
    private static string currentNickname = "Player"; // Default nickname
    
    private void Start()
    {
        // Load saved nickname when starting
        LoadNickname();
        
        // Set up input field events
        if (nicknameInputField != null)
        {
            nicknameInputField.onEndEdit.AddListener(OnNicknameChanged);
            nicknameInputField.text = currentNickname;
        }
        
        // Set up save button if exists
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveNickname);
        }
    }
    
    private void OnNicknameChanged(string newNickname)
    {
        // Called when player finishes editing the input field
        if (!string.IsNullOrEmpty(newNickname) && newNickname.Trim().Length > 0)
        {
            currentNickname = newNickname.Trim();
            SaveNickname();
        }
    }
    
    public void SaveNickname()
    {
        // Save nickname to PlayerPrefs (persistent storage)
        if (nicknameInputField != null)
        {
            string nickname = nicknameInputField.text.Trim();
            if (!string.IsNullOrEmpty(nickname))
            {
                currentNickname = nickname;
                PlayerPrefs.SetString(NICKNAME_KEY, currentNickname);
                PlayerPrefs.Save();
                
                Debug.Log($"Nickname saved: {currentNickname}");
            }
        }
    }
    
    private void LoadNickname()
    {
        // Load nickname from PlayerPrefs
        currentNickname = PlayerPrefs.GetString(NICKNAME_KEY, "Player");
        Debug.Log($"Nickname loaded: {currentNickname}");
    }
    
    // Static method to get current nickname from anywhere
    public static string GetCurrentNickname()
    {
        return currentNickname;
    }
    
    // Method to set nickname programmatically
    public static void SetNickname(string newNickname)
    {
        if (!string.IsNullOrEmpty(newNickname))
        {
            currentNickname = newNickname.Trim();
            PlayerPrefs.SetString(NICKNAME_KEY, currentNickname);
            PlayerPrefs.Save();
        }
    }
}