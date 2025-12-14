using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.IO;
using Baiss.Infrastructure.Db;

namespace Baiss.UI.Configuration;

/// <summary>
/// Configuration for application logging using Serilog
/// </summary>
public static class LoggingConfiguration
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly object _lock = new object();

    /// <summary>
    /// Configures and creates a logger factory for the application
    /// </summary>
    /// <returns>Configured logger factory</returns>
    public static ILoggerFactory CreateLoggerFactory()
    {
        if (_loggerFactory != null)
            return _loggerFactory;

        lock (_lock)
        {
            if (_loggerFactory != null)
                return _loggerFactory;

        // Get the application directory for log files
        var appDirectory = Path.GetDirectoryName(DbConstants.DatabasePath) ?? AppContext.BaseDirectory;
        var logDirectory = Path.Combine(appDirectory, "logs");

        // Ensure logs directory exists
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "baiss-.log");

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Changé de Information à Debug
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Create logger factory
        _loggerFactory = new SerilogLoggerFactory(Log.Logger);
        return _loggerFactory;
        }
    }

    /// <summary>
    /// Creates a logger for a specific type
    /// </summary>
    /// <typeparam name="T">The type to create logger for</typeparam>
    /// <returns>Configured logger</returns>
    public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        var loggerFactory = CreateLoggerFactory();
        return loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger with a specific category name
    /// </summary>
    /// <param name="categoryName">The category name for the logger</param>
    /// <returns>Configured logger</returns>
    public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        var loggerFactory = CreateLoggerFactory();
        return loggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    /// Ensures proper cleanup of logging resources
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
