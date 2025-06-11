using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Application = System.Windows.Application;
using System.Windows.Media;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.Themes;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing application themes
/// </summary>
public class ThemeService : IThemeService
{
    // Windows API enums and methods
    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20, // Windows 10 1809+
        DWMWA_CAPTION_COLOR = 35 // Added in Windows 11
    }

    private string _currentThemeName;
    private readonly IMessengerService _messengerService;
    private readonly ISettingsService _settingsService;

    public ThemeService(IMessengerService messengerService, ISettingsService settingsService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Load themes from file (creates file with defaults if missing)
        LoadThemesFromFile();

        // Load theme name from settings
        _currentThemeName = _settingsService.CurrentTheme ?? "Dark";

        // Subscribe to color changes for the current theme
        if (CurrentTheme != null)
            CurrentTheme.PropertyChanged += OnThemeColorChanged;
    }

    /// <summary>
    /// Gets the current theme name
    /// </summary>
    public string CurrentThemeName => _currentThemeName;

    /// <summary>
    /// Gets the current Theme object instance
    /// </summary>
    public Theme? CurrentTheme => ThemeCollection.GetTheme(_currentThemeName);

    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        if (!ThemeCollection.ThemeExists(themeName))
            themeName = ThemeCollection.DarkTheme.ThemeName; // Fallback to Dark theme if invalid

        // Unsubscribe from previous theme color changes
        if (CurrentTheme != null)
            CurrentTheme.PropertyChanged -= OnThemeColorChanged;

        _currentThemeName = themeName;
        _settingsService.CurrentTheme = themeName;

        var appResources = Application.Current.Resources;
        var currentTheme = ThemeCollection.GetTheme(themeName);

        // Clear existing theme resources
        foreach (var key in currentTheme.ThemeColors.Keys)
            if (appResources.Contains(key)) appResources.Remove(key);

        foreach (var key in currentTheme.ThemeBrushes.Keys)
            if (appResources.Contains(key)) appResources.Remove(key);

        // First add all color resources
        foreach (var kvp in currentTheme.ThemeColors)
            appResources[kvp.Key] = kvp.Value;

        // Then add all brush resources
        foreach (var kvp in currentTheme.ThemeBrushes)
            appResources[kvp.Key] = kvp.Value;

        // Subscribe to new theme color changes
        if (CurrentTheme != null)
            CurrentTheme.PropertyChanged += OnThemeColorChanged;

        // Notify via messaging
        _messengerService.Send(new ThemeChangedMessage(themeName));
    }

    /// <summary>
    /// Applies the current theme to a window's title bar
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="fallbackBrush">Optional fallback brush to use if theme color isn't available</param>
    public void ApplyTitleBarTheme(IntPtr hwnd, SolidColorBrush? fallbackBrush = null)
    {
        try
        {
            // Set dark mode for title bar based on current theme
            bool isDarkTheme = CurrentThemeName == ThemeCollection.DarkTheme.ThemeName;
            uint darkModeValue = isDarkTheme ? 1u : 0u;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(uint));

            // Get the background color from the current theme
            if (CurrentTheme?.ThemeColors.TryGetValue("PrimaryBackgroundColor", out Color bgColor) == true)
            {
                // Convert to win32 COLORREF format (BGR)
                uint colorRef = (uint)((bgColor.R) | (bgColor.G << 8) | (bgColor.B << 16));

                // Set title bar color via DwmApi
                DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
            }
            else if (fallbackBrush != null)
            {
                // Fallback to provided brush if theme color isn't available
                uint colorRef = (uint)((fallbackBrush.Color.R) | (fallbackBrush.Color.G << 8) | (fallbackBrush.Color.B << 16));
                DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
            }
        }
        catch
        {
            // Fail silently if API not supported on this Windows version
        }
    }

    /// <summary>
    /// Gets the name of the current theme applied to the application
    /// </summary>
    public string GetCurrentThemeFromResources()
    {
        return _currentThemeName;
    }

    /// <summary>
    /// Initializes the theme from saved settings
    /// </summary>
    public void InitializeTheme()
    {
        try
        {
            // Apply the theme from settings
            ApplyTheme(_currentThemeName);

            // Log initialization
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Theme initialized: {_currentThemeName}");
        }
        catch (Exception ex)
        {
            // Fallback to Dark theme if there's an error
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error initializing theme: {ex.Message}");
            ApplyTheme(ThemeCollection.DarkTheme.ThemeName);
        }
    }

    public static void LoadThemesFromFile()
    {
        string path = GetThemesFilePath();
        if (!File.Exists(path))
        {
            SaveThemesToFile(); // Save defaults if not present
            return;
        }

        string json = File.ReadAllText(path);
        Dictionary<string, Dictionary<string, string>>? loaded = null;
        bool invalid = false;

        try
        {
            loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (loaded == null)
                invalid = true;
        }
        catch
        {
            invalid = true;
        }

        if (invalid)
        {
            // Backup corrupt file
            string bakPath = path + ".bak";
            if (File.Exists(bakPath)) File.Delete(bakPath);
            File.Move(path, bakPath);
            // Restore defaults
            SaveThemesToFile();
            return;
        }

        if (loaded == null) return;

        foreach (var themeKvp in loaded)
        {
            var colorDict = new Dictionary<string, Color>();
            foreach (var colorKvp in themeKvp.Value)
            {
                colorDict[colorKvp.Key] = (Color)ColorConverter.ConvertFromString(colorKvp.Value);
            }

            if (ThemeCollection.Themes.ContainsKey(themeKvp.Key))
            {
                ThemeCollection.Themes[themeKvp.Key].ThemeColors = colorDict;
            }
            else
            {
                ThemeCollection.Themes[themeKvp.Key] = new Theme(themeKvp.Key)
                {
                    ThemeColors = colorDict
                };
            }
        }
    }

    public static void SaveThemesToFile()
    {
        var themesToSave = ThemeCollection.Themes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ThemeColors.ToDictionary(
                c => c.Key,
                c => c.Value.ToString() // Store as hex string
            )
        );

        string json = JsonSerializer.Serialize(themesToSave, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetThemesFilePath(), json);
    }

    /// <summary>
    /// Toggles between available themes
    /// </summary>
    public void ToggleTheme()
    {
        ApplyTheme(_currentThemeName == ThemeCollection.DarkTheme.ThemeName ? ThemeCollection.LightTheme.ThemeName : ThemeCollection.DarkTheme.ThemeName);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, int cbAttribute);

    private static string GetThemesFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "WmiExplorer");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, "themes.json");
    }

    private void OnThemeColorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && e.PropertyName.StartsWith("Item["))
        {
            SaveThemesToFile();
            ApplyTheme(_currentThemeName); // Refresh theme
        }
    }
}