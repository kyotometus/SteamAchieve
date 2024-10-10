using SteamKit2;

namespace SteamAchieve.Steam
{
    public class Auth
    {
        private readonly SteamClient steamClient;
        private readonly CallbackManager manager;
        private readonly SteamUser steamUser;
        private bool isLoggedIn = false;

        public Auth()
        {
            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();

            // Subscribe to callbacks
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        }

        public void ConnectAndLogin(string username, string password)
        {
            Console.WriteLine("Connecting to Steam...");
            steamClient.Connect();

            while (!isLoggedIn)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Connected to Steam. Logging in...");
            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = "YourUsername", // replace with your username
                Password = "YourPassword"  // replace with your password
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected from Steam.");
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                Console.WriteLine("Successfully logged in to Steam!");
                isLoggedIn = true;
            }
            else
            {
                Console.WriteLine($"Failed to log in: {callback.Result}");
                isLoggedIn = false;
            }
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off from Steam.");
            isLoggedIn = false;
        }
    }
}