using System.Net;
using System.Text;

namespace SubrosaServerEmulator.Web;

public class WebServer(ServerSettings settings) : LogSource, IDisposable
{
    public void Run()
    {
        var prefix = $"http://+:{settings.WebServerListenPort}/";
        Debug($"Initializing web server at {prefix}");
        
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                try
                {
                    HandleRequest(ctx);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    Error(ex.Message);
                }
            }
        });
        
        Info($"Web server started at {prefix}");
    }
    public void Stop() => _listener.Stop();
    public void Dispose()
    {
        if (_listener.IsListening) Stop();
        _listener.Close();
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        Debug($"Received request from {ctx.Request.RemoteEndPoint}");
        string[] fields =
        [
            "38", // Expected version
            "",
            settings.MasterPublicIP,
            (settings.MasterListenPort - 2).ToString(),
            "", "", "",
            "", "", "", "",
        ];

        byte[] body = Encoding.ASCII.GetBytes(string.Join('\t', fields));

        ctx.Response.ContentType = "text/plain";
        ctx.Response.ContentLength64 = body.Length;
        ctx.Response.OutputStream.Write(body, 0, body.Length);
        ctx.Response.OutputStream.Close();
    }

    protected override string SourceName => "Web Server";

    private readonly HttpListener _listener = new();
}