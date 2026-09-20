using System.Net;
using System.Text;

namespace SubrosaServerEmulator.Web;

public class WebServer(ServerSettings settings) : LogSource, IDisposable
{
    public void Run()
    {
        var prefix = $"http://+:{settings.WebServerListenPort}/";
        Debug($"Initializing web server at {prefix}");

        _body = BuildBody();

        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _acceptLoop = Task.Run(AcceptLoopAsync);

        Info($"Web server started at {prefix}");
    }

    public void Stop()
    {
        if (_listener.IsListening)
            _listener.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* ignore */ }

        _listener.Close();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (HttpListenerException) when (!_listener.IsListening)
                {
                    break;
                }
                catch (HttpListenerException ex)
                {
                    Warning(ex.Message);
                    await Task.Delay(100).ConfigureAwait(false);
                    continue;
                }

                HandleRequest(ctx);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            Error(ex.Message);
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var response = ctx.Response;
        try
        {
            response.ContentType = "text/plain";
            response.ContentLength64 = _body.Length;
            response.OutputStream.Write(_body, 0, _body.Length);
            response.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            Error(ex.Message);
            
            response.Abort();
        }
    }

    private byte[] BuildBody()
    {
        string[] fields =
        [
            "38",
            "",
            settings.MasterPublicIP,
            (settings.MasterListenPort - 2).ToString(),
            "", "", "",
            "", "", "", "",
        ];

        return Encoding.ASCII.GetBytes(string.Join('\t', fields));
    }

    protected override string SourceName => "Web Server";

    private readonly HttpListener _listener = new();
    private byte[] _body = [];
    private Task? _acceptLoop;
    private bool _disposed;
}