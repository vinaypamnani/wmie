using System.Windows.Media;
using WmiExplorer.Presentation.Themes;

namespace WmiExplorer.Services;

/// <summary>
/// Service interface for managing application themes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme name
    /// </summary>
    string CurrentThemeName { get; }

    /// <summary>
    /// Gets the current Theme object instance
    /// </summary>
    Theme? CurrentThemeObject { get; }

    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    /// <param name="themeName">Name of the theme to apply</param>
    void ApplyTheme(string themeName);

    /// <summary>
    /// Toggles between available themes
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// Initializes the theme from saved settings
    /// </summary>
    void InitializeTheme();

    /// <summary>
    /// Applies the current theme to a window's title bar
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="fallbackBrush">Optional fallback brush to use if theme color isn't available</param>
    void ApplyTitleBarTheme(IntPtr hwnd, SolidColorBrush? fallbackBrush = null);

    /// <summary>
    /// Gets the name of the current theme applied to the application
    /// </summary>
    /// <returns>Current theme name</returns>
    string GetCurrentThemeFromResources();
}
