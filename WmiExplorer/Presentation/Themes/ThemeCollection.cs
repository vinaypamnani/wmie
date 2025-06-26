using System.Windows.Media;

namespace WmiExplorer.Presentation.Themes;

/// <summary>
/// Collection of predefined application themes with static instances
/// </summary>
public static class ThemeCollection
{
    // Immutable default themes for restoring
    public static readonly Theme DefaultDarkTheme;

    public static readonly Theme DefaultLightTheme;

    /// <summary>
    /// Dictionary of all available themes for easy lookup and modification
    /// </summary>
    private static readonly Dictionary<string, Theme> _themes;

    static ThemeCollection()
    {
        // Ensure DarkTheme and LightTheme are initialized before using them
        DefaultDarkTheme = new Theme("Dark_Default")
        {
            ThemeColors = new Dictionary<string, Color>(DarkTheme.ThemeColors)
        };

        DefaultLightTheme = new Theme("Light_Default")
        {
            ThemeColors = new Dictionary<string, Color>(LightTheme.ThemeColors)
        };

        _themes = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dark"] = DarkTheme,
            ["Light"] = LightTheme
        };
    }

    /// <summary>
    /// Dark theme with refined dark palette for better depth and readability
    /// </summary>
    public static Theme DarkTheme { get; } = new Theme("Dark")
    {
        ThemeColors = new Dictionary<string, Color>
        {
            // Base colors - Brightened for vibrancy on dark backgrounds
            ["BaseGreen"] = (Color)ColorConverter.ConvertFromString("#FF4CD964"),  // Apple-style green
            ["BaseRed"] = (Color)ColorConverter.ConvertFromString("#FFFF5E5E"),    // Soft but distinct red
            ["BaseOrange"] = (Color)ColorConverter.ConvertFromString("#FFFFB340"), // Warm amber
            ["BaseGray"] = (Color)ColorConverter.ConvertFromString("#FFBFBFBF"),   // Lighter neutral gray
            ["BaseBlue"] = (Color)ColorConverter.ConvertFromString("#FF5CAEFF"),   // Refined soft blue

            // Background Colors - Even stepping for dark theme
            ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF181818"), // near-black
            ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF292929"), // dark gray
            ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF343434"), // medium dark gray
            ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF404040"), // lighter dark gray

            // Foreground Colors - Even stepping for dark theme
            ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF5F5F5"),   // near-white
            ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFBFBFBF"), // light gray
            ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF8A8A8A"),  // medium gray
            ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF555555"),  // muted gray

            // Accent Colors - Eye-catching and accessible
            ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF4169E1"),  // royal blue
            ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF5478E4"), // lighter variant

            // Border & Structural Colors - Clear but subtle
            ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FF555555"), // subtle border for dark theme
        }
    };

    /// <summary>
    /// Gets the default theme (Dark theme)
    /// </summary>
    public static Theme Default => DarkTheme;

    /// <summary>
    /// Light theme with clean whites and modern blue accents
    /// </summary>
    public static Theme LightTheme { get; } = new Theme("Light")
    {
        ThemeColors = new Dictionary<string, Color>
        {
            // Base Colors - Slightly softened for better balance on light background
            ["BaseGreen"] = (Color)ColorConverter.ConvertFromString("#FF28A745"), // vibrant accessible green
            ["BaseRed"] = (Color)ColorConverter.ConvertFromString("#FFD13438"), // brighter red
            ["BaseOrange"] = (Color)ColorConverter.ConvertFromString("#FFFFAA44"), // warm amber
            ["BaseGray"] = (Color)ColorConverter.ConvertFromString("#FF666666"), // darker gray for better separation
            ["BaseBlue"] = (Color)ColorConverter.ConvertFromString("#FF0078D4"), // accessible vivid blue

            // Background Colors - Clear visual layering
            ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"), // white
            ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF5F5F5"), // very light gray
            ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFEEEEEE"), // light gray
            ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFE5E5E5"), // muted light gray

            // Foreground Colors - High contrast and clarity
            ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF333333"), // lighter almost black for better contrast
            ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF666666"), // lighter strong gray
            ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF999999"), // lighter medium gray
            ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFCCCCCC"), // lighter muted gray for disabled text

            // Accent Colors - Aligned for modern UI standards
            ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF87CEEB"), // sky blue
            ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF79B9D3"), // darker variant

            // Border Color - Subtle but defined
            ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FFCCCCCC"), // subtle border for light theme
        }
    };

    /// <summary>
    /// Gets all available theme names
    /// </summary>
    public static IEnumerable<string> ThemeNames => _themes.Keys;

    /// <summary>
    /// Gets the modifiable theme dictionary for runtime access
    /// </summary>
    public static Dictionary<string, Theme> Themes => _themes;

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
}