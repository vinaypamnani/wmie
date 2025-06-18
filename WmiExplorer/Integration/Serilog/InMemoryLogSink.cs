using Serilog.Core;
using Serilog.Events;
using System.Windows;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Models;

namespace WmiExplorer.Integration.Serilog;

/// <summary>
/// Custom Serilog sink that stores log events in memory for UI display
/// </summary>
public class InMemoryLogSink : ILogEventSink, IDisposable
{
    private readonly object _lockObject = new();
    private readonly Queue<LogEntry> _logEntries = new();
    private readonly int _maxEntries;
    private readonly Action<LogEntry> _onLogEntry;

    /// <summary>
    /// Initializes a new instance of the InMemoryLogSink class
    /// </summary>
    /// <param name="onLogEntry">Callback to handle new log entries</param>
    /// <param name="maxEntries">Maximum number of entries to keep in memory</param>
    public InMemoryLogSink(Action<LogEntry> onLogEntry, int maxEntries = Log.MaxInMemoryLogEntries)
    {
        _onLogEntry = onLogEntry ?? throw new ArgumentNullException(nameof(onLogEntry));
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Emits a log event to the sink
    /// </summary>
    /// <param name="logEvent">The log event to emit</param>
    public void Emit(LogEvent logEvent)
    {
        if (_disposed || logEvent == null)
            return;

        try
        {
            var logEntry = CreateLogEntry(logEvent);

            // Store the log entry in our buffer
            lock (_lockObject)
            {
                _logEntries.Enqueue(logEntry);

                // Remove old entries if we exceed the limit
                while (_logEntries.Count > _maxEntries)
                {
                    _logEntries.Dequeue();
                }
            }

            // Ensure UI updates happen on the UI thread
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                Application.Current.Dispatcher.BeginInvoke(() => _onLogEntry(logEntry));
            }
            else
            {
                _onLogEntry(logEntry);
            }
        }
        catch (Exception ex)
        {
            // Avoid infinite loops by not logging errors from the logging system
            System.Diagnostics.Debug.WriteLine($"[InMemoryLogSink] Error processing log event: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all currently stored log entries
    /// </summary>
    /// <returns>A copy of all stored log entries</returns>
    public IEnumerable<LogEntry> GetStoredEntries()
    {
        lock (_lockObject)
        {
            return _logEntries.ToArray();
        }
    }

    /// <summary>
    /// Converts Serilog LogEventLevel to our LogLevel enum
    /// </summary>
    /// <param name="level">The Serilog log level</param>
    /// <returns>Our LogLevel enum value</returns>
    private static LogLevel ConvertLogLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error or LogEventLevel.Fatal => LogLevel.Error,
            _ => LogLevel.Information
        };
    }

    /// <summary>
    /// Creates a LogEntry from a Serilog LogEvent
    /// </summary>
    /// <param name="logEvent">The Serilog log event</param>
    /// <returns>A LogEntry for UI display</returns>
    private static LogEntry CreateLogEntry(LogEvent logEvent)
    {
        // Extract caller information from our CallerEnricher
        string source = "Unknown";
        if (logEvent.Properties.TryGetValue("Caller", out var callerValue))
        {
            source = callerValue.ToString().Trim('"');
        }

        // Check if this is a system message
        bool isSystemMessage = false;
        if (logEvent.Properties.TryGetValue("IsSystemMessage", out var systemMsgValue))
        {
            isSystemMessage = systemMsgValue.ToString().Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        // For system messages, prefix the source to make them easily identifiable
        var message = logEvent.RenderMessage();
        if (isSystemMessage)
        {
            message = $"[SYSTEM] {message}";
        }

        return new LogEntry
        {
            Timestamp = logEvent.Timestamp.DateTime,
            Level = ConvertLogLevel(logEvent.Level),
            Source = source,
            Message = message,
            Exception = logEvent.Exception?.ToString()
        };
    }

    #region IDisposable
    private bool _disposed;

    /// <summary>
    /// Disposes the sink
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    #endregion
}