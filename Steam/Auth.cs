using SteamKit2;

namespace SteamAchieve.Steam
{
    public class Auth
    {
        private readonly SteamClient steamClient;
        private readonly CallbackManager manager;
        private readonly SteamUser steamUser;
        private bool isLoggedIn = false;
        private TaskCompletionSource<bool> loginTaskCompletionSource;
        
        private string _username;
        private string _password;
        
        public bool IsLoggedIn
        {
            get { return isLoggedIn; }
            set => isLoggedIn = value;
        }

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

        public async Task ConnectAndLoginAsync(string username, string password)
        {
            // Store username and password for later use
            _username = username;
            _password = password;

            if (steamClient.IsConnected)
            {
                Console.WriteLine("Already connected to Steam. Disconnecting to retry...");
                steamClient.Disconnect();
                await Task.Delay(1000); // Wait a bit for disconnect to complete
            }

            loginTaskCompletionSource = new TaskCompletionSource<bool>();
            Console.WriteLine("Connecting to Steam...");
            steamClient.Connect();

            // Poll callback manager while waiting for login to complete
            while (!loginTaskCompletionSource.Task.IsCompleted)
            {
                manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
                await Task.Delay(100); // Ensure the loop doesn't block the async context entirely
            }

            await loginTaskCompletionSource.Task; // Wait for the login result
        }


        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Connected to Steam. Logging in...");
            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = _username, // Use the stored username
                Password = _password  // Use the stored password
            });
        }


        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected from Steam.");
            IsLoggedIn = false;

            // If disconnected while waiting for login, mark the task as completed
            loginTaskCompletionSource.TrySetResult(false);
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                Console.WriteLine("Successfully logged in to Steam!");
                isLoggedIn = true;
                loginTaskCompletionSource.TrySetResult(true);
            }
            else
            {
                Console.WriteLine($"Failed to log in: {callback.Result}");
                isLoggedIn = false;
                loginTaskCompletionSource.TrySetResult(false);

                // Disconnect after a failed login
                steamClient.Disconnect();
            }
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off from Steam.");
            IsLoggedIn = false;
        }
    }
}