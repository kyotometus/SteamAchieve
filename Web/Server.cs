using System.Net;
using System.Text;

namespace SteamAchieve.Web
{
    public class Server
    {
        private HttpListener _listener;
        private string _url = "http://localhost:8080/";
        // private string _htmlFilePath = "Views/index.html";
        
        // Development file path
        private string _htmlFilePath = "/Users/mc/Desktop/personal/SteamAchieve/Web/Views/index.html";

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

                // Check if the request is for the root URL
                if (request.Url.AbsolutePath == "/")
                {
                    string html = File.ReadAllText(_htmlFilePath);
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 404;
                    byte[] buffer = Encoding.UTF8.GetBytes("404 - Not Found");
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }

                response.Close();
            }
        }
    }
}