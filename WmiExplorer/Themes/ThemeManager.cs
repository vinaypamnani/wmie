using System.Windows;
using System.Windows.Media;
using WmiExplorer.Services;
using WmiExplorer.Common.Shared;
using Application = System.Windows.Application;
using System.IO;
using System.Text.Json;

namespace WmiExplorer.Themes
{
    /// <summary>
    /// Event arguments for theme changes
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public ThemeChangedEventArgs(string theme)
        {
            Theme = theme;
        }

        public string Theme { get; }
    }

    /// <summary>
    /// Manager for application themes
    /// </summary>
    public class ThemeManager
    {
        private readonly IMessagingService _messagingService;
        private readonly ISettingsService _settingsService;
        private string _currentThemeName;

        private static readonly ThemeCollection Themes = new ThemeCollection
        {
            ["Dark"] = new Theme("Dark")
            {
                ThemeColors = new Dictionary<string, Color>
                {
                    ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF181818"),
                    ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF1F1F1F"),
                    ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FF1F1F1F"),
                    ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF0F0F0"),
                    ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF0F0F0"),
                    ["ReadOnlyForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFB0B0B0"),
                    ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF0078D4"),                    
                    ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF5B7EC7"),
                    ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FF454545"),
                    ["SuccessColor"] = (Color)ColorConverter.ConvertFromString("#FF36B536"),
                    ["ErrorColor"] = (Color)ColorConverter.ConvertFromString("#FFEC4D39"),
                    ["WarningColor"] = (Color)ColorConverter.ConvertFromString("#FFFFC14F"),
                    ["IndeterminateColor"] = (Color)ColorConverter.ConvertFromString("#FFA0A0A0"),
                    ["BusyColor"] = (Color)ColorConverter.ConvertFromString("#FFFFB347"),
                    ["NoFocusColor"] = (Color)ColorConverter.ConvertFromString("#FF2A2A2A")
                }
            },
            ["Light"] = new Theme("Light")
            {
                ThemeColors = new Dictionary<string, Color>
                {
                    ["PrimaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"),
                    ["SecondaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF8F9FA"),
                    ["TertiaryBackgroundColor"] = (Color)ColorConverter.ConvertFromString("#FFF0F1F3"),
                    ["PrimaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF202020"),
                    ["SecondaryForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FF202020"),
                    ["ReadOnlyForegroundColor"] = (Color)ColorConverter.ConvertFromString("#FFB0B0B0"),
                    ["PrimaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF0078D4"),                    
                    ["SecondaryAccentColor"] = (Color)ColorConverter.ConvertFromString("#FF5B87C5"),
                    ["BorderColor"] = (Color)ColorConverter.ConvertFromString("#FFD8D8D8"),
                    ["SuccessColor"] = (Color)ColorConverter.ConvertFromString("#FF107C10"),
                    ["ErrorColor"] = (Color)ColorConverter.ConvertFromString("#FFD13438"),
                    ["WarningColor"] = (Color)ColorConverter.ConvertFromString("#FFFF8C00"),
                    ["IndeterminateColor"] = (Color)ColorConverter.ConvertFromString("#FF767676"),
                    ["BusyColor"] = (Color)ColorConverter.ConvertFromString("#FF0063B1"),
                    ["NoFocusColor"] = (Color)ColorConverter.ConvertFromString("#FFD0D0D0")
                }
            }
        };

        static ThemeManager()
        {
            // No need to set ThemeBrushes here; ThemeColors setter handles it
        }

        public ThemeManager(IMessagingService messagingService, ISettingsService settingsService)
        {
            _messagingService = messagingService;
            _settingsService = settingsService;

            // Load themes from file (creates file with defaults if missing)
            LoadThemesFromFile();

            // Load theme name from settings
            _currentThemeName = _settingsService.CurrentTheme ?? "Dark";

            // Subscribe to color changes for the current theme
            if (CurrentThemeObject != null)
                CurrentThemeObject.PropertyChanged += OnThemeColorChanged;
        }

        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Gets the current theme name
        /// </summary>
        public string CurrentThemeName => _currentThemeName;

        /// <summary>
        /// Gets the current Theme object instance
        /// </summary>
        public Theme? CurrentThemeObject => Themes.TryGetValue(_currentThemeName, out var theme) ? theme : null;

        /// <summary>
        /// Raises the ThemeChanged event
        /// </summary>
        protected virtual void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ThemeChanged?.Invoke(this, e);
        }

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
            _settingsService.CurrentTheme = themeName;

            var appResources = Application.Current.Resources;
            // Remove all theme brushes/colors
            foreach (var key in Themes[themeName].ThemeColors.Keys)
                if (appResources.Contains(key)) appResources.Remove(key);
            foreach (var key in Themes[themeName].ThemeBrushes.Keys)
                if (appResources.Contains(key)) appResources.Remove(key);

            // Add new theme colors
            foreach (var kvp in Themes[themeName].ThemeColors)
                appResources[kvp.Key] = kvp.Value;
            foreach (var kvp in Themes[themeName].ThemeBrushes)
                appResources[kvp.Key] = kvp.Value;

            // Subscribe to new theme color changes
            if (CurrentThemeObject != null)
                CurrentThemeObject.PropertyChanged += OnThemeColorChanged;

            // Notify via messaging
            _messagingService.Publish(new ThemeChangedMessage(themeName));
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

        /// <summary>
        /// Toggles between available themes
        /// </summary>
        public void ToggleTheme()
        {
            ApplyTheme(_currentThemeName == "Dark" ? "Light" : "Dark");
        }

        private static string GetThemesFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "WmiExplorer");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "themes.json");
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

        private void OnThemeColorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null && e.PropertyName.StartsWith("Item["))
            {
                // Extract color key from property name
                var key = e.PropertyName.Substring(5, e.PropertyName.Length - 6);
                var color = CurrentThemeObject?[key].ToString() ?? string.Empty;
                SaveThemesToFile();
                ApplyTheme(_currentThemeName); // Refresh theme
            }
        }
    }
}