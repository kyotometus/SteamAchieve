using SteamAchieve.Web;
using SteamAchieve.Steam;

class Program
{
    static async Task Main(string[] args)
    {
        var server = new Server();
        var serverTask = Task.Run(() => server.Start());

        await serverTask;
    }
}