using System.ComponentModel;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Represents a single log entry for display in the UI
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the exception details, if any
    /// </summary>
    [Category("Exception")]
    [ShowChildrenAsParent]
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the string representation of the exception for display
    /// </summary>
    [Category("Log Entry")]
    public string? ExceptionText => Exception?.ToString();

    /// <summary>
    /// Gets whether this log entry has exception details
    /// </summary>
    [Browsable(false)]
    public bool HasException => Exception != null;

    /// <summary>
    /// Gets or sets the log level
    /// </summary>
    [Category("Log Entry")]
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
    [Category("Log Entry")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source context (typically the class name)
    /// </summary>
    [Category("Log Entry")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the log entry was created
    /// </summary>
    [Category("Log Entry")]
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