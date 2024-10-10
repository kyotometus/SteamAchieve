using System.Net;
using System.Text;
using SteamAchieve.Steam;
using SteamKit2;
using SteamKit2.Authentication;
using System.Threading.Tasks;
using System.IO;

namespace SteamAchieve.Web
{
    public class Server
    {
        private HttpListener _listener;
        private string _url = "http://localhost:8080/";
        private string _htmlFilePath = "C:\\Users\\persa\\Desktop\\SteamAchieve\\Web\\Views\\index.html";
        private WebAuthenticator _webAuthenticator = new WebAuthenticator();
        private AuthSession _authSession;

        private SteamClient steamClient;
        private CallbackManager manager;
        private SteamUser steamUser;

        // TaskCompletionSource to manage the state of the connection
        private TaskCompletionSource<bool> _connectionCompletionSource;

        public Server()
        {
            // Initialize Steam client and related components
            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();

            // Subscribe to necessary callbacks
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            // Start running the callback manager in a background task
            Task.Run(() => RunCallbackManager());
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(_url);
            _listener.Start();
            Console.WriteLine($"Server started at {_url}");

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            _listener.Close();
        }

        private async Task HandleIncomingConnections()
        {
            while (true)
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string path = request.Url.AbsolutePath.ToLower();

                if (request.HttpMethod == "GET" && path == "/")
                {
                    await HandleIndex(response);
                }
                else if (request.HttpMethod == "POST" && path == "/login")
                {
                    await HandleLogin(request, response);
                }
                else
                {
                    await HandleNotFound(response);
                }
            }
        }

        // Handle serving the index.html for GET request
        private async Task HandleIndex(HttpListenerResponse response)
        {
            string html = File.ReadAllText(_htmlFilePath);
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        // Handle POST request to /login
        private async Task HandleLogin(HttpListenerRequest request, HttpListenerResponse response)
        {
            string formData = await GetRequestData(request);
            string username = ParseFormData(formData, "username");
            string password = ParseFormData(formData, "password");
            string twoFactorCode = ParseFormData(formData, "twoFactorCode");
            string steamGuardCode = ParseFormData(formData, "steamGuardCode");

            try
            {
                if (_authSession == null)
                {
                    Console.WriteLine($"Attempting initial login for user: {username}");

                    int retryCount = 3;

                    while (retryCount > 0)
                    {
                        _connectionCompletionSource = new TaskCompletionSource<bool>();

                        // Connect to Steam
                        steamClient.Connect();

                        // Await connection completion asynchronously
                        if (await WaitForSteamConnectionAsync(TimeSpan.FromSeconds(10)))
                        {
                            Console.WriteLine("Connected to Steam successfully.");
                            break; // Exit retry loop on success
                        }

                        Console.WriteLine("Failed to connect to Steam: Connection timed out. Retrying...");
                        retryCount--;
                    }

                    if (retryCount == 0)
                    {
                        Console.WriteLine("Failed to connect to Steam after multiple attempts.");
                        await SendResponse(response, "<html><body><h1>Failed to connect to Steam. Please try again later.</h1></body></html>");
                        return;
                    }

                    // Begin the authentication session after a successful connection
                    _authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                    {
                        Username = username,
                        Password = password,
                        Authenticator = new UserConsoleAuthenticator(), // Use console authenticator for initial testing
                        IsPersistentSession = false,
                    });

                    // Start polling for the authentication result
                    var pollResponse = await _authSession.PollingWaitForResultAsync();

                    // Log on to Steam with the access token we received
                    steamUser.LogOn(new SteamUser.LogOnDetails
                    {
                        Username = pollResponse.AccountName,
                        AccessToken = pollResponse.RefreshToken,
                        ShouldRememberPassword = false,
                    });

                    await SendResponse(response, "<html><body><h1>Login successful!</h1></body></html>");
                }
                else if (!string.IsNullOrEmpty(twoFactorCode))
                {
                    // Provide the 2FA code from the user
                    _webAuthenticator.ProvideDeviceCode(twoFactorCode);
                }
                else if (!string.IsNullOrEmpty(steamGuardCode))
                {
                    // Provide the Steam Guard code from the user
                    _webAuthenticator.ProvideEmailCode(steamGuardCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");

                if (ex is SteamGuardRequiredException)
                {
                    await SendSteamGuardForm(response);
                }
                else if (ex is TwoFactorCodeRequiredException)
                {
                    await SendTwoFactorForm(response);
                }
                else
                {
                    await SendResponse(response, "<html><body><h1>Login failed! Please try again.</h1></body></html>");
                }
            }
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Connected to Steam!");
            _connectionCompletionSource?.SetResult(true);
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine($"Disconnected from Steam. UserInitiated: {callback.UserInitiated}");
            if (!callback.UserInitiated)
            {
                // Retry connection if disconnected unexpectedly
                _connectionCompletionSource?.SetResult(false);
            }
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                Console.WriteLine("Successfully logged on to Steam!");
            }
            else
            {
                Console.WriteLine($"Unable to log on to Steam: {callback.Result}");
                _connectionCompletionSource?.SetResult(false);
            }
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine($"Logged off from Steam: {callback.Result}");
            _connectionCompletionSource?.SetResult(false);
        }

        // Utility method to wait for the connection
        private async Task<bool> WaitForSteamConnectionAsync(TimeSpan timeout)
        {
            var completedTask = await Task.WhenAny(_connectionCompletionSource.Task, Task.Delay(timeout));
            return completedTask == _connectionCompletionSource.Task && _connectionCompletionSource.Task.Result;
        }

        // Utility method to run the callback manager
        private void RunCallbackManager()
        {
            while (true)
            {
                manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            }
        }

        // Helper method to send the Steam Guard code form
        private async Task SendSteamGuardForm(HttpListenerResponse response)
        {
            string htmlResponse = @"
                <!DOCTYPE html>
                <html>
                    <body>
                        <h1>Steam Guard Code Required</h1>
                        <form action='/login' method='post'>
                            <label for='steamGuardCode'>Enter the code you received in your email:</label>
                            <input type='text' id='steamGuardCode' name='steamGuardCode' required />
                            <button type='submit'>Submit Code</button>
                        </form>
                    </body>
                </html>";
            await SendResponse(response, htmlResponse);
        }

        // Helper method to send the two-factor authentication form
        private async Task SendTwoFactorForm(HttpListenerResponse response)
        {
            string htmlResponse = @"
                <!DOCTYPE html>
                <html>
                    <body>
                        <h1>Two-Factor Authentication Required</h1>
                        <form action='/login' method='post'>
                            <label for='twoFactorCode'>Enter the code from your Steam app:</label>
                            <input type='text' id='twoFactorCode' name='twoFactorCode' required />
                            <button type='submit'>Submit Code</button>
                        </form>
                    </body>
                </html>";
            await SendResponse(response, htmlResponse);
        }

        // Utility method to handle 404 Not Found
        private async Task HandleNotFound(HttpListenerResponse response)
        {
            response.StatusCode = 404;
            await SendResponse(response, "<html><body><h1>404 - Not Found</h1></body></html>");
        }

        // Utility method to get POST data from request
        private async Task<string> GetRequestData(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        // Utility method to parse form data
        private string ParseFormData(string formData, string key)
        {
            var pairs = formData.Split('&');
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2 && keyValue[0] == key)
                {
                    return Uri.UnescapeDataString(keyValue[1]);
                }
            }
            return string.Empty;
        }

        // Utility method to send a response
        private async Task SendResponse(HttpListenerResponse response, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}