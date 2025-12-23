namespace WmiExplorer.Presentation.ViewModels.Helpers
{
    /// <summary>
    /// Helper methods for parsing command parameters.
    /// </summary>
    public static class CommandParameterHelper
    {
        /// <summary>
        /// Attempts to parse a bool value from an object parameter.
        /// Returns the defaultValue if parsing fails.
        /// </summary>
        /// <param name="parameter">The parameter to parse.</param>
        /// <param name="defaultValue">The default value to use if parsing fails.</param>
        /// <returns>The parsed bool value or the default value.</returns>
        public static bool ParseBool(object? parameter, bool defaultValue = true)
        {
            if (parameter is bool b)
                return b;
            if (parameter is string s && bool.TryParse(s, out var parsed))
                return parsed;
            return defaultValue;
        }
    }
}
