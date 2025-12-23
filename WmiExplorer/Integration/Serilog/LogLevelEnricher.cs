using Serilog.Core;
using Serilog.Events;

namespace WmiExplorer.Integration.Serilog;

/// <summary>
/// Custom Serilog enricher that adds the numeric log level value
/// </summary>
public class LogLevelEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches the log event with the numeric log level
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Convert Serilog's LogEventLevel to our LogLevel integer values
        var numericLevel = logEvent.Level switch
        {
            LogEventLevel.Verbose => 0,   // Debug
            LogEventLevel.Debug => 0,     // Debug
            LogEventLevel.Information => 1, // Information
            LogEventLevel.Warning => 2,   // Warning
            LogEventLevel.Error => 3,     // Error
            LogEventLevel.Fatal => 3,     // Error
            _ => 1                       // Default to Information
        };

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("NumericLevel", numericLevel));
    }
}