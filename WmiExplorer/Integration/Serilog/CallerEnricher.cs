using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WmiExplorer.Integration.Serilog;

/// <summary>
/// Custom Serilog enricher that adds caller information by analyzing the stack trace.
/// Specifically designed to skip our Log wrapper class and find the actual calling method.
/// Optimized for performance with minimal allocations and early exit strategies.
/// </summary>
public class CallerEnricher : ILogEventEnricher
{
    // Pre-compiled constants to avoid string allocations
    private static readonly string AsyncStateMachineIndicator = "+<";

    private static readonly string AsyncStateMachineSuffix = ">d__";
    private static readonly string EnricherType = "CallerEnricher";
    private static readonly string LogNamespace = "WmiExplorer.Common.Logging.Log";
    private static readonly string SerilogNamespace = "Serilog.";

    /// <summary>
    /// Enriches the log event with caller information
    /// </summary>
    /// <param name="logEvent">The log event to enrich</param>
    /// <param name="propertyFactory">Factory for creating log event properties</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var caller = GetCaller();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Caller", caller));
    }

    /// <summary>
    /// Extracts the original method name and declaring class from an async state machine type name
    /// </summary>
    /// <param name="stateMachineTypeName">The full type name of the async state machine</param>
    /// <returns>The formatted ClassName.MethodName string, or null if extraction fails</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? ExtractAsyncMethodInfo(string stateMachineTypeName)
    {
        // Example: "WmiExplorer.Services.WmiService+<GetRootNamespaceAsync>d__18"
        // We want to extract: "WmiService.GetRootNamespaceAsync"

        var plusIndex = stateMachineTypeName.LastIndexOf('+');
        if (plusIndex == -1) return null;

        // Get the declaring class part: "WmiExplorer.Services.WmiService"
        var declaringClassFullName = stateMachineTypeName.AsSpan(0, plusIndex);

        // Extract just the class name from the full namespace
        var lastDotIndex = declaringClassFullName.LastIndexOf('.');
        var className = lastDotIndex >= 0
            ? declaringClassFullName.Slice(lastDotIndex + 1)
            : declaringClassFullName;

        // Extract the method name from the state machine name: "<GetRootNamespaceAsync>d__18"
        var stateMachineName = stateMachineTypeName.AsSpan(plusIndex + 1);

        // Find the method name between < and >
        var openBracket = stateMachineName.IndexOf('<');
        var closeBracket = stateMachineName.IndexOf('>');

        if (openBracket >= 0 && closeBracket > openBracket)
        {
            var methodName = stateMachineName.Slice(openBracket + 1, closeBracket - openBracket - 1);
            return $"{className}.{methodName}";
        }

        return null;
    }

    /// <summary>
    /// Gets the actual caller by examining the stack trace to skip our Log wrapper methods
    /// and other framework internals. Optimized for performance with early exits and minimal allocations.
    /// </summary>
    /// <returns>The actual caller in ClassName.MethodName format</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetCaller()
    {
        try
        {
            // Create stack trace without file info for better performance
            var stackTrace = new StackTrace(false);
            var frameCount = stackTrace.FrameCount;

            // Start at frame 2 to skip Enrich() and GetCaller() methods
            for (int i = 2; i < frameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null) continue;

                var method = frame.GetMethod();
                if (method?.DeclaringType == null) continue;

                var declaringType = method.DeclaringType;
                var typeName = declaringType.FullName;
                if (typeName == null) continue;

                var methodName = method.Name;

                // Fast path: Check for our specific exclusions using StartsWith for better performance
                if (typeName.StartsWith(LogNamespace) ||
                    typeName.StartsWith(SerilogNamespace) ||
                    typeName.Contains(EnricherType))
                {
                    continue;
                }

                // Check if this is an async state machine (fast check first)
                if (typeName.Contains(AsyncStateMachineIndicator))
                {
                    if (typeName.Contains(AsyncStateMachineSuffix))
                    {
                        var asyncInfo = ExtractAsyncMethodInfo(typeName);
                        if (asyncInfo != null)
                        {
                            return asyncInfo;
                        }
                    }
                    continue;
                }

                // Skip other compiler-generated methods
                if (methodName.IndexOf('<') >= 0 || methodName.IndexOf('>') >= 0)
                {
                    continue;
                }

                // Extract class name efficiently
                var lastDotIndex = typeName.LastIndexOf('.');
                var className = lastDotIndex >= 0
                    ? typeName.AsSpan(lastDotIndex + 1)
                    : typeName.AsSpan();

                return $"{className}.{methodName}";
            }

            return "Unknown";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CallerEnricher] Error getting caller from stack trace: {ex.Message}");
            return "Unknown";
        }
    }
}