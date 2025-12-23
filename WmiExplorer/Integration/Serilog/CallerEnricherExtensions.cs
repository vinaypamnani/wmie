using Serilog;
using Serilog.Configuration;

namespace WmiExplorer.Integration.Serilog;

/// <summary>
/// Extension methods for configuring the CallerEnricher
/// </summary>
public static class CallerEnricherExtensions
{
    /// <summary>
    /// Adds the CallerEnricher to the logger configuration.
    /// This enricher analyzes the stack trace to find the actual calling method,
    /// skipping our Log wrapper class and framework internals.
    /// </summary>
    /// <param name="enrichmentConfiguration">The enrichment configuration</param>
    /// <returns>The modified logger configuration</returns>
    public static LoggerConfiguration WithCaller(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.With<CallerEnricher>();
    }
}