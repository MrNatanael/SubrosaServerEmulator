using System.Diagnostics;
using System.Text.Json;

namespace SubrosaServerEmulator;

public class ServerSettings
{
    public static ServerSettings LoadFrom(string path)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            IndentSize = 2
        };
        
        if (!File.Exists(path))
        {
            var cfg = new ServerSettings();
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(cfg, jsonOptions));
            }
            catch (Exception)
            {
                Debug.WriteLine("Unable to generate settings file!");
            }

            return cfg;
        }
        
        return JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(path), jsonOptions)!;
    }
    
    public string MasterListenIP { get; set; } = "127.0.0.1";
    public int MasterListenPort { get; set; } = 27592;
    public string MasterPublicIP { get; set; } = "127.0.0.1";
    
    public string WebServerListenIP { get; set; } = "127.0.0.1";
    public int WebServerListenPort { get; set; } = 80;
    
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;
}