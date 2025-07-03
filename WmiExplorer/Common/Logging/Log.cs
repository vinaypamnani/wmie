using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;
using WmiExplorer.Common.Models;
using WmiExplorer.Integration.Serilog;

namespace WmiExplorer.Common.Logging;

/// <summary>
/// Global static logger for the application.
/// Provides simple, clean logging methods that automatically capture caller information.
///
/// This class centralizes all logging configuration and setup, including:
/// - Custom CallerEnricher for accurate stack trace analysis
/// - File and in-memory sink configuration
/// - Fallback logging in case of file system issues
/// - Dynamic log level switching through LoggingLevelSwitch
/// </summary>
public static class Log
{
    /// <summary>
    /// Event raised when a new log entry is added (for UI integration)
    /// </summary>
    public static event Action<LogEntry>? LogEntryAdded;

    /// <summary>
    /// Maximum number of log entries to keep in memory for UI display
    /// </summary>
    public const int MaxInMemoryLogEntries = 1000;

    private static InMemoryLogSink _inMemoryLogSink = null!;
    private static LoggingLevelSwitch _levelSwitch = null!;
    private static string? _logFilePath;
    private static ILogger _logger = null!;

    /// <summary>
    /// Gets the current log file path
    /// </summary>
    public static string LogFilePath => _logFilePath ?? "Unknown";

    /// <summary>
    /// Configure and initialize the logging system
    /// </summary>
    public static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WmiExplorer");

        _logFilePath = Path.Combine(logDirectory, "WmiExplorer.log");

        // Create the logging level switch for dynamic level control
        _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

        // Create the in-memory sink for UI integration
        var inMemoryLogSink = new InMemoryLogSink(OnLogEntryAdded, maxEntries: MaxInMemoryLogEntries);
        _inMemoryLogSink = inMemoryLogSink;

        try
        {
            // Ensure log directory exists
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Configure Serilog with our custom CallerEnricher and LoggingLevelSwitch
            // Our CallerEnricher analyzes the stack trace to find actual calling methods,
            // properly skipping our Log wrapper class and framework internals.
            var logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .Enrich.FromLogContext()
                .Enrich.WithCaller()
                .Enrich.With<LogLevelEnricher>() // Add our custom enricher to include numeric log level
                .WriteTo.File(
                    path: _logFilePath,
                    fileSizeLimitBytes: 5 * 1024 * 1024, // 5MB
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 1,
                    shared: true,
                    outputTemplate: "<![LOG[{Message:lj}{Exception}]LOG]!><time=\"{Timestamp:HH:mm:ss.fff}\" date=\"{Timestamp:MM-dd-yyyy}\" component=\"{Caller}\" context=\"\" type=\"{NumericLevel}\" thread=\"\" file=\"\">{NewLine}")
                .WriteTo.Sink(inMemoryLogSink)
                .CreateLogger();

            // Initialize our global logger
            Initialize(logger);

            // Log successful initialization
            Information("Logging system initialized. Log file: {LogFilePath}", _logFilePath);
        }
        catch (Exception ex)
        {
            // Fallback to minimal logging if file logging fails
            var fallbackLogger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .Enrich.FromLogContext()
                .Enrich.WithCaller()
                .Enrich.With<LogLevelEnricher>() // Add our custom enricher for consistency
                .WriteTo.Sink(inMemoryLogSink)
                .CreateLogger();

            Initialize(fallbackLogger);
            Error(ex, "Failed to configure file logging, using in-memory only");
        }
    }

    /// <summary>
    /// Log debug information
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Debug(string messageTemplate, params object[] propertyValues)
        => _logger.Debug(messageTemplate, propertyValues);

    /// <summary>
    /// Log errors
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Error(string messageTemplate, params object[] propertyValues)
        => _logger.Error(messageTemplate, propertyValues);

    /// <summary>
    /// Log errors with exception
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        => _logger.Error(exception, messageTemplate, propertyValues);

    /// <summary>
    /// Gets all currently stored log entries from the in-memory sink
    /// </summary>
    /// <returns>A collection of stored log entries</returns>
    public static IEnumerable<LogEntry> GetStoredLogEntries()
    {
        return _inMemoryLogSink?.GetStoredEntries() ?? Enumerable.Empty<LogEntry>();
    }

    /// <summary>
    /// Log general information
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Information(string messageTemplate, params object[] propertyValues)
        => _logger.Information(messageTemplate, propertyValues);

    /// <summary>
    /// Initialize the global logger (called once at app startup)
    /// </summary>
    /// <param name="logger">The configured Serilog logger instance</param>
    public static void Initialize(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the minimum log level dynamically for the entire application
    /// </summary>
    /// <param name="logLevel">The minimum log level to set</param>
    public static void SetMinimumLevel(LogLevel logLevel)
    {
        if (_levelSwitch == null)
        {
            throw new InvalidOperationException("Logging system not initialized. Call ConfigureLogging() first.");
        }

        var serilogLevel = logLevel switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };

        _levelSwitch.MinimumLevel = serilogLevel;

        // Log system message that bypasses filtering
        SystemInformation("Minimum log level changed to {LogLevel}", logLevel);
    }

    /// <summary>
    /// Log warnings
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Warning(string messageTemplate, params object[] propertyValues)
        => _logger.Warning(messageTemplate, propertyValues);

    /// <summary>
    /// Log warnings with exception
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    public static void Warning(Exception exception, string messageTemplate, params object[] propertyValues)
        => _logger.Warning(exception, messageTemplate, propertyValues);

    /// <summary>
    /// Internal method called by the InMemoryLogSink to raise the LogEntryAdded event
    /// </summary>
    /// <param name="entry">The log entry that was added</param>
    internal static void OnLogEntryAdded(LogEntry entry)
    {
        LogEntryAdded?.Invoke(entry);
    }

    /// <summary>
    /// Logs a system message that always appears regardless of minimum log level.
    /// These messages are logged at Warning level but marked as system messages.
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="propertyValues">Values for the message template</param>
    private static void SystemInformation(string messageTemplate, params object[] propertyValues)
    {
        // Temporarily store the current minimum level
        var currentLevel = _levelSwitch?.MinimumLevel ?? LogEventLevel.Debug;

        // Temporarily lower the minimum level to ensure our system message gets through
        if (_levelSwitch != null)
        {
            _levelSwitch.MinimumLevel = LogEventLevel.Debug;
        }

        // Log the system message with a special property to identify it
        _logger.ForContext("IsSystemMessage", true)
               .Information(messageTemplate, propertyValues);

        // Restore the original minimum level
        if (_levelSwitch != null)
        {
            _levelSwitch.MinimumLevel = currentLevel;
        }
    }
}