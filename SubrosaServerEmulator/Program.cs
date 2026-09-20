using System.Diagnostics;
using System.Runtime.InteropServices;
using SubrosaServerEmulator;
using SubrosaServerEmulator.Master;
using SubrosaServerEmulator.Web;
// ReSharper disable AccessToDisposedClosure

var settings = ServerSettings.LoadFrom("config.json");

LogSource.MinimumLogLevel = settings.LogLevel;

using var master = new MasterServer();
using var webserver = new WebServer(settings);

using var shutdown = new ManualResetEventSlim(false);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Set();
};

using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
{
    ctx.Cancel = true;
    shutdown.Set();
});

try
{
    master.Run(settings);
    webserver.Run();

    shutdown.Wait();
}
catch (Exception ex)
{
    Debug.WriteLine(ex);
    master.Error(ex.Message);
    Environment.ExitCode = 1;
}