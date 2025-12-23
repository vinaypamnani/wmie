namespace WmiExplorer.Common.Helpers;

/// <summary>
/// Helper class for parsing command-line arguments.
/// Supports both -key VALUE and -key=VALUE formats.
/// </summary>
public static class CommandLineArgumentsParser
{
    /// <summary>
    /// Gets a specific argument value by key (case-insensitive).
    /// </summary>
    /// <param name="args">Command-line arguments array</param>
    /// <param name="key">The argument key to look for (without the leading dash)</param>
    /// <returns>The argument value, or null if not found</returns>
    public static string? GetValue(string[] args, string key)
    {
        var parsed = Parse(args);
        return parsed.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    /// <summary>
    /// Checks if a specific flag argument is present (case-insensitive).
    /// </summary>
    /// <param name="args">Command-line arguments array</param>
    /// <param name="key">The argument key to check for (without the leading dash)</param>
    /// <returns>True if the flag is present, false otherwise</returns>
    public static bool HasFlag(string[] args, string key)
    {
        var parsed = Parse(args);
        return parsed.ContainsKey(key);
    }

    /// <summary>
    /// Parses command-line arguments and returns a dictionary of key-value pairs.
    /// Supports both -key VALUE and -key=VALUE formats.
    /// </summary>
    /// <param name="args">Command-line arguments array</param>
    /// <returns>Dictionary of parsed arguments (keys are case-insensitive)</returns>
    public static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Check for -key=VALUE format
            if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase) && arg.Contains('='))
            {
                var parts = arg.Split(new[] { '=' }, 2, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var key = parts[0].TrimStart('-').Trim();
                    var value = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        result[key] = value;
                    }
                }
            }
            // Check for -key VALUE format
            else if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase))
            {
                var key = arg.TrimStart('-').Trim();
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.OrdinalIgnoreCase))
                {
                    var value = args[i + 1].Trim();
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        result[key] = value;
                    }
                    i++; // Skip next argument as it's the value
                }
                else if (!string.IsNullOrWhiteSpace(key))
                {
                    // Flag without value (e.g., -debug)
                    result[key] = string.Empty;
                }
            }
        }

        return result;
    }
}