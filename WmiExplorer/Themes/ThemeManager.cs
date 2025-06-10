using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Application = System.Windows.Application;
using System.Windows.Media;
using WmiExplorer.Services;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Themes;

/// <summary>
/// Manager for application themes
/// </summary>
public class ThemeManager
{
    // Windows API enums and methods
    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20, // Windows 10 1809+
        DWMWA_CAPTION_COLOR = 35 // Added in Windows 11
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, int cbAttribute);

    private string _currentThemeName;
    private readonly IMessengerService _messengerService;
    private readonly ISettingsService _settingsService;

    private static readonly ThemeCollection Themes = new ThemeCollection
    {
        ["Dark"] = new Theme("Dark")
        {
            ThemeColors = new Dictionary<string, Color>
            {
                // Background Colors - Refined dark theme palette with better depth
                ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF121212"),
                ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF1E1E1E"),
                ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF252525"),
                ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF2D2D2D"),

                // Foreground Colors - High contrast with softer white for better readability
                ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFEBEBEB"),
                ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFD4D4D4"),
                ["DisabledForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF6D6D6D"),

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
        },
        ["Light"] = new Theme("Light")
        {
            ThemeColors = new Dictionary<string, Color>
            {
                // Background Colors - Clean whites with subtle gray variations for depth
                ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"),
                ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF5F5F5"),
                ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFEBEBEB"),
                ["DisabledBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF0F0F0"),

                // Foreground Colors - Dark grays for better contrast and readability
                ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF202020"),
                ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF505050"),
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
        }
    };

    public ThemeManager(IMessengerService messengerService, ISettingsService settingsService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Load themes from file (creates file with defaults if missing)
        LoadThemesFromFile();

        // Load theme name from settings
        _currentThemeName = _settingsService.CurrentTheme ?? "Dark";

        // Subscribe to color changes for the current theme
        if (CurrentThemeObject != null)
            CurrentThemeObject.PropertyChanged += OnThemeColorChanged;
    }

    static ThemeManager()
    {
        // No need to set ThemeBrushes here; ThemeColors setter handles it
    }

    /// <summary>
    /// Gets the current theme name
    /// </summary>
    public string CurrentThemeName => _currentThemeName;

    /// <summary>
    /// Gets the current Theme object instance
    /// </summary>
    public Theme? CurrentThemeObject => Themes.TryGetValue(_currentThemeName, out var theme) ? theme : null;

    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        if (!Themes.ContainsKey(themeName))
            themeName = "Dark";

        // Unsubscribe from previous theme color changes
        if (CurrentThemeObject != null)
            CurrentThemeObject.PropertyChanged -= OnThemeColorChanged;

        _currentThemeName = themeName;
        _settingsService.CurrentTheme = themeName; var appResources = Application.Current.Resources;

        // Clear existing theme resources
        foreach (var key in Themes[themeName].ThemeColors.Keys)
            if (appResources.Contains(key)) appResources.Remove(key);

        foreach (var key in Themes[themeName].ThemeBrushes.Keys)
            if (appResources.Contains(key)) appResources.Remove(key);

        // First add all color resources
        foreach (var kvp in Themes[themeName].ThemeColors)
            appResources[kvp.Key] = kvp.Value;

        // Then add all brush resources
        foreach (var kvp in Themes[themeName].ThemeBrushes)
            appResources[kvp.Key] = kvp.Value;

        // Subscribe to new theme color changes
        if (CurrentThemeObject != null)
            CurrentThemeObject.PropertyChanged += OnThemeColorChanged;

        // Notify via messaging
        _messengerService.Send(new ThemeChangedMessage(themeName));
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
            System.Diagnostics.Debug.WriteLine($"Theme initialized: {_currentThemeName}");
        }
        catch (Exception ex)
        {
            // Fallback to Dark theme if there's an error
            System.Diagnostics.Debug.WriteLine($"Error initializing theme: {ex.Message}");
            ApplyTheme("Dark");
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
            if (Themes.ContainsKey(themeKvp.Key))
            {
                Themes[themeKvp.Key].ThemeColors = colorDict;
            }
            else
            {
                Themes[themeKvp.Key] = new Theme(themeKvp.Key)
                {
                    ThemeColors = colorDict
                };
            }
        }
    }

    public static void SaveThemesToFile()
    {
        var themesToSave = Themes.ToDictionary(
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
        ApplyTheme(_currentThemeName == "Dark" ? "Light" : "Dark");
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
            bool isDarkTheme = CurrentThemeName == "Dark";
            uint darkModeValue = isDarkTheme ? 1u : 0u;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(uint));

            // Get the background color from the current theme
            if (CurrentThemeObject?.ThemeColors.TryGetValue("PrimaryBackgroundColor", out Color bgColor) == true)
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