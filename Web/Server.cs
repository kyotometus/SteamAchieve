using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using SteamAchieve.Steam;

namespace SteamAchieve.Web
{
    public class Server
    {
        private HttpListener _listener;
        private string _url = "http://localhost:8080/";
        private string _htmlFilePath = "C:\\Users\\persa\\Desktop\\SteamAchieve\\Web\\Views\\index.html";
        private Auth _auth;

        public Server()
        {
            _auth = new Auth();
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

            Console.WriteLine($"Login attempt: {username} / {password}");

            // Simulate login logic here
            await _auth.ConnectAndLoginAsync(username, password);
            if (_auth.IsLoggedIn)
            {
                await SendResponse(response, "<html><body><h1>Login successful!</h1></body></html>");
            }
            else
            {
                await SendResponse(response, "<html><body><h1>Login failed!</h1></body></html>");
            }
        }
        
        // Handle 404 Not Found
        private async Task HandleNotFound(HttpListenerResponse response)
        {
            response.StatusCode = 404;
            await SendResponse(response, "<html><body><h1>404 - Not Found</h1></body></html>");
        }

        // Utility method to read the POST data from the request
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
