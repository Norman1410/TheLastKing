// Lobby/RelayGameStarter.cs
using System.Threading.Tasks;

public class RelayGameStarter : IGameStarter
{
    public async void StartHost()
    {
        // Llama a tus métodos actuales (no los modifiques)
        await RelayStartHelpers.CreateAndStartHost();
    }

    public async void StartClient(string joinCode)
    {
        await RelayStartHelpers.JoinAsClient(joinCode);
    }
}
