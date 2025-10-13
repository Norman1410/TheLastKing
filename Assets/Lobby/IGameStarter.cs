// Lobby/IGameStarter.cs
public interface IGameStarter
{
    void StartHost();                // Host crea partida
    void StartClient(string token);  // Cliente se une (IP si LAN, joinCode si Relay)
}
