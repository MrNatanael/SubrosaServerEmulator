namespace SubrosaServerEmulator;

public abstract class LogSource
{
    public void Debug(string msg) => Log(msg, LogLevel.Debug);
    public void Info(string msg) => Log(msg, LogLevel.Info);
    public void Warning(string msg) => Log(msg, LogLevel.Warning);
    public void Error(string msg) => Log(msg, LogLevel.Error);
    
    public void Log(string message, LogLevel level)
    {
        if((int)level < (int)MinimumLogLevel) return;

        Console.ForegroundColor = level switch
        {
            LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.DarkYellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor. White
        };
        LogWriter.WriteLine($"{DateTime.Now:dd/MM/yyyy hh:mm:ss} [{level}] {SourceName}: {message}");
    }
    
    protected abstract string SourceName { get; }
    
    public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
    public static TextWriter LogWriter { get; set; } = Console.Out;
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}