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

        // Background Colors - Refined layering and contrast
        ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF1E1E1E"), // near-black
        ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF2C2C2E"), // deep gray
        ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF3A3A3C"), // medium dark
        ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF4A4A4C"), // slightly lighter

        // Foreground Colors - Maintain clarity without eye strain
        ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF5F5F5"),   // near-white
        ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFD0D0D0"), // light gray
        ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF9E9E9E"),  // medium gray
        ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF646464"),  // muted gray

        // Accent Colors - Eye-catching and accessible
        ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF409CFF"),  // clean blue
        ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF62B4FF"), // lighter variant

        // Border & Structural Colors - Clear but subtle
        ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FF4D4D4D"), // neutral border
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
        ["BaseGreen"] = (Color)ColorConverter.ConvertFromString("#FF107C10"), // Softer green
        ["BaseRed"] = (Color)ColorConverter.ConvertFromString("#FFD13438"), // Brighter red
        ["BaseOrange"] = (Color)ColorConverter.ConvertFromString("#FFFFAA44"), // Warm amber
        ["BaseGray"] = (Color)ColorConverter.ConvertFromString("#FF8A8A8A"), // Slightly lighter
        ["BaseBlue"] = (Color)ColorConverter.ConvertFromString("#FF005FB8"), // Slightly deeper blue

        // Background Colors - Clear visual layering
        ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"), // white
        ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF2F4F7"), // subtle cool gray
        ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFE6E9ED"), // mid gray-blue
        ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFDADDE1"), // muted neutral

        // Foreground Colors - High contrast and clarity
        ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF111111"), // almost black
        ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF444444"), // strong gray
        ["TertiaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF777777"), // medium gray
        ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFA0A0A0"), // muted gray

        // Accent Colors - Aligned for modern UI standards
        ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF0067C0"), // accessible blue
        ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF4AA0E2"), // lighter variant

        // Border Color - Subtle but defined
        ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FFCACACA"), // neutral light gray
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