using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;

namespace YongChuanTools.Logging
{
    public static class SerilogSetup
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        public static string LogDir => LogDirectory;

        public static LoggerConfiguration BuildConfig(bool verbose = false)
        {
            Directory.CreateDirectory(LogDirectory);
            var logLevel = verbose ? LogEventLevel.Debug : LogEventLevel.Information;

            return new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("App", "YongChuanTools")
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Literate,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(LogDirectory, "yongchuan-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    encoding: System.Text.Encoding.UTF8);
        }

        public static void Initialize(bool verbose = false)
        {
            Log.Logger = BuildConfig(verbose).CreateLogger();
            Log.Information("=== YongChuanTools 启动 ===");
            Log.Information("日志目录: {LogDir}", LogDirectory);
        }

        public static void Shutdown()
        {
            Log.Information("=== YongChuanTools 关闭 ===");
            Log.CloseAndFlush();
        }
    }
}
