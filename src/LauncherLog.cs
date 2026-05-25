using Serilog;
using Serilog.Events;

namespace ObeliskLauncher;

static class LauncherLog
{
    static bool s_initialized;

    public static void Initialize()
    {
        if (s_initialized)
            return;

        s_initialized = true;

        string logsDir = Path.Combine(LauncherPlatform.Current.AppDataFolder, "logs");
        Directory.CreateDirectory(logsDir);
        string filePath = Path.Combine(logsDir, "obelisklauncher-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAndFlush();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Error(ex, "Unhandled process exception");
            else
                Error("Unhandled process exception: {ExceptionObject}", args.ExceptionObject);

            CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        Information("Logger initialized. Logs directory: {LogsDir}", logsDir);
    }

    public static void Debug(string messageTemplate, params object?[] args) => Log.Debug(messageTemplate, args);

    public static void Information(string messageTemplate, params object?[] args) => Log.Information(messageTemplate, args);

    public static void Warning(string messageTemplate, params object?[] args) => Log.Warning(messageTemplate, args);

    public static void Error(string messageTemplate, params object?[] args) => Log.Error(messageTemplate, args);

    public static void Error(Exception ex, string messageTemplate, params object?[] args) => Log.Error(ex, messageTemplate, args);

    public static void CloseAndFlush() => Log.CloseAndFlush();
}
