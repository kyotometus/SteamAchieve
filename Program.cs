using SteamAchieve.Web;
using SteamAchieve.Steam;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new Server();
        var auth = new Auth();

        var authTask = Task.Run(() => auth.ConnectAndLogin("Username", "Password"));
        var serverTask = Task.Run(() => server.Start());

        await Task.WhenAll(authTask, serverTask);
    }
}