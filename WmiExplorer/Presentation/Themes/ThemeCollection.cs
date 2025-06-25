using System.Windows.Media;

namespace WmiExplorer.Presentation.Themes;

/// <summary>
/// Collection of predefined application themes with static instances
/// </summary>
public static class ThemeCollection
{
    /// <summary>
    /// Dark theme with refined dark palette for better depth and readability
    /// </summary>
    public static Theme DarkTheme { get; } = new Theme("Dark")
    {
        ThemeColors = new Dictionary<string, Color>
        {
            // Background Colors - Improved contrast for better distinction
            ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF181A1B"),
            ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF232526"),
            ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF313335"),
            ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF393B3D"),

            // Foreground Colors - High contrast and clear hierarchy
            ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF3F3F3"),
            ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFCCCCCC"),
            ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF9A9A9A "),
            ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF5A5A5A"),

            // Accent Colors - More saturated blue for better visibility in dark theme
            ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF3B9CFF"),
            ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF60AFFF"),

            // Border & Structural Colors - More distinct for better UI definition
            ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FF3D3D3D"),

            // Status Colors - More vibrant for better visibility
            ["SuccessColor"] = (Color)ColorConverter.ConvertFromString("#FF42D642"),
            ["ErrorColor"] = (Color)ColorConverter.ConvertFromString("#FFFF4D4D"),
            ["WarningColor"] = (Color)ColorConverter.ConvertFromString("#FFFFCC00"),
            ["IndeterminateColor"] = (Color)ColorConverter.ConvertFromString("#FFB3B3B3"),
            ["BusyColor"] = (Color)ColorConverter.ConvertFromString("#FF69B5FF")
        }
    };

    /// <summary>
    /// Light theme with clean whites and modern blue accents
    /// </summary>
    public static Theme LightTheme { get; } = new Theme("Light")
    {
        ThemeColors = new Dictionary<string, Color>
        {
            // Background Colors - Improved contrast for better distinction
            ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"), // pure white
            ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF6F8FA"), // very light gray-blue
            ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFEDEFF1"), // light gray
            ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFE5E7E9"), // slightly darker, for disabled

            // Foreground Colors - Dark grays for better contrast and readability
            ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF1A1A1A"),
            ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF505050"),
            ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF8A8A8A"),
            ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFB0B0B0"),

            // Accent Colors - Modern blue palette with better contrast
            ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF0078D7"),
            ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF3A96DD"),

            // Border & Structural Colors - Soft borders for clean appearance
            ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FFD0D0D0"),

            // Status Colors - More saturated for better visibility while maintaining light theme aesthetic
            ["SuccessColor"] = (Color)ColorConverter.ConvertFromString("#FF0F7B0F"),
            ["ErrorColor"] = (Color)ColorConverter.ConvertFromString("#FFE81123"),
            ["WarningColor"] = (Color)ColorConverter.ConvertFromString("#FFFF8C00"),
            ["IndeterminateColor"] = (Color)ColorConverter.ConvertFromString("#FF767676"),
            ["BusyColor"] = (Color)ColorConverter.ConvertFromString("#FF0063B1")
        }
    };

    /// <summary>
    /// Dictionary of all available themes for easy lookup and modification
    /// </summary>
    private static readonly Dictionary<string, Theme> _themes = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase)
    {
        ["Dark"] = DarkTheme,
        ["Light"] = LightTheme
    };

    /// <summary>
    /// Gets the modifiable theme dictionary for runtime access
    /// </summary>
    public static Dictionary<string, Theme> Themes => _themes;

    /// <summary>
    /// Gets all available theme names
    /// </summary>
    public static IEnumerable<string> ThemeNames => _themes.Keys;

    /// <summary>
    /// Gets a theme by name, returns Dark theme as fallback
    /// </summary>
    /// <param name="themeName">Name of the theme to retrieve</param>
    /// <returns>The requested theme or Dark theme as fallback</returns>
    public static Theme GetTheme(string themeName)
    {
        return _themes.TryGetValue(themeName, out var theme) ? theme : DarkTheme;
    }

    /// <summary>
    /// Checks if a theme with the specified name exists
    /// </summary>
    /// <param name="themeName">Name of the theme to check</param>
    /// <returns>True if the theme exists, false otherwise</returns>
    public static bool ThemeExists(string themeName)
    {
        return _themes.ContainsKey(themeName);
    }

    /// <summary>
    /// Gets the default theme (Dark theme)
    /// </summary>
    public static Theme Default => DarkTheme;
}