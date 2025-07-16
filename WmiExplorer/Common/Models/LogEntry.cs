using System.ComponentModel;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Represents a single log entry for display in the UI
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the exception details, if any
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Gets the formatted timestamp for display
    /// </summary>
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    /// <summary>
    /// Gets whether this log entry has exception details
    /// </summary>
    public bool HasException => !string.IsNullOrEmpty(Exception);

    /// <summary>
    /// Gets or sets the log level
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets the display text for the log level
    /// </summary>
    [Browsable(false)]
    public string LevelText => Level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        _ => "UNKNOWN"
    };

    /// <summary>
    /// Gets or sets the log message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source context (typically the class name)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the log entry was created
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Log level enumeration for our application logging
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}