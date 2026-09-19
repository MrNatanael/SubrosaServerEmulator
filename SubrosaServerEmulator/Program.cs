using System.Diagnostics;
using SubrosaServerEmulator;
using SubrosaServerEmulator.Master;
using SubrosaServerEmulator.Web;

var settings = ServerSettings.LoadFrom("config.json");

LogSource.MinimumLogLevel = settings.LogLevel;

var master = new MasterServer();
var webserver = new WebServer(settings);

try
{
    master.Run(settings);
    webserver.Run();
}
catch (Exception ex)
{
    Debug.WriteLine(ex);
    master.Error(ex.Message);
}

while (true);