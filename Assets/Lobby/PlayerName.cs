using UnityEngine;

public static class PlayerName
{
    const string Key = "player_name";

    public static string Get() => PlayerPrefs.GetString(Key, "Jugador");
    public static string GetOr(string fallback) => PlayerPrefs.GetString(Key, string.IsNullOrWhiteSpace(fallback) ? "Jugador" : fallback);
    public static void Set(string name)
    {
        PlayerPrefs.SetString(Key, string.IsNullOrWhiteSpace(name) ? "Jugador" : name.Trim());
        PlayerPrefs.Save();
    }
}
