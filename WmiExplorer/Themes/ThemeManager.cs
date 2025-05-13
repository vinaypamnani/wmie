using System.Windows;
using System.Windows.Media;
using WmiExplorer.Services;
using Application = System.Windows.Application;

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
        private readonly ISettingsService? _settingsService;
        private string _currentTheme;
        private string _themeToggleText;

        public ThemeManager(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService;

            // Initialize with default values
            _currentTheme = _settingsService?.CurrentTheme ?? "Dark";
            _themeToggleText = _currentTheme == "Dark" ? "🌙 Dark" : "🌞 Light";
        }

        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Gets the current theme name
        /// </summary>
        public string CurrentTheme => _currentTheme;

        /// <summary>
        /// Gets the text to display for the theme toggle button
        /// </summary>
        public string ThemeToggleText => _themeToggleText;

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
            if (string.IsNullOrEmpty(themeName) || (themeName != "Dark" && themeName != "Light"))
            {
                themeName = "Dark"; // Default to Dark if invalid theme name
            }

            // Update internal state
            _currentTheme = themeName;
            _themeToggleText = _currentTheme == "Dark" ? "🌙 Dark" : "🌞 Light";

            var appResources = Application.Current.Resources.MergedDictionaries;

            // Find existing theme dictionary
            var existingTheme = appResources.FirstOrDefault(d =>
                d.Source != null &&
                ((d.Source.OriginalString.Contains("Colors/Dark.xaml") || d.Source.OriginalString.Contains("Colors/Light.xaml"))));

            // Remove existing theme dictionary
            if (existingTheme != null)
            {
                appResources.Remove(existingTheme);
            }

            // Add new theme dictionary with the correct path to Themes folder
            var newTheme = new ResourceDictionary
            {
                Source = new Uri($"Themes/Colors/{themeName}.xaml", UriKind.Relative)
            };
            appResources.Add(newTheme);

            // Apply user accent color if set
            string? userAccent = null;
            if (_settingsService != null)
            {
                var property = _settingsService.GetType().GetProperty("PrimaryAccentColor");
                if (property != null)
                {
                    userAccent = property.GetValue(_settingsService) as string;
                }
            }
            if (!string.IsNullOrWhiteSpace(userAccent))
            {
                var color = (Color)ColorConverter.ConvertFromString(userAccent);
                Application.Current.Resources["PrimaryAccentColor"] = color;
                // Generate a secondary accent color (darker shade)
                var secondaryAccent = Color.FromArgb(
                    color.A,
                    (byte)(color.R * 0.7),
                    (byte)(color.G * 0.7),
                    (byte)(color.B * 0.7));
                Application.Current.Resources["SecondaryAccentColor"] = secondaryAccent;

                // If transparent, use fallback for selected item brushes
                bool isTransparent = color.A == 0;
                Color fallbackSelected = _currentTheme == "Dark"
                    ? (Color)ColorConverter.ConvertFromString("#FF444444") // dark gray
                    : (Color)ColorConverter.ConvertFromString("#FFD0D0D0"); // light gray
                SolidColorBrush selectedBrush = isTransparent ? new SolidColorBrush(fallbackSelected) : new SolidColorBrush(color);

                Application.Current.Resources["PrimaryAccentBrush"] = new SolidColorBrush(color);                
                Application.Current.Resources["SelectedItemBackgroundBrush"] = selectedBrush;
                Application.Current.Resources["HoverBackgroundBrush"] = isTransparent ? new SolidColorBrush(fallbackSelected) { Opacity = 0.5 } : new SolidColorBrush(color) { Opacity = 0.5 };
                Application.Current.Resources["ScrollBarThumbHoverBrush"] = isTransparent ? new SolidColorBrush(fallbackSelected) { Opacity = 0.5 } : new SolidColorBrush(color) { Opacity = 0.5 };
                Application.Current.Resources["ScrollBarThumbPressedBrush"] = isTransparent ? new SolidColorBrush(fallbackSelected) { Opacity = 0.5 } : new SolidColorBrush(color) { Opacity = 0.5 };
                Application.Current.Resources["PropertyGridAccentBrush"] = selectedBrush;
                Application.Current.Resources["PropertyGridSelectedBackgroundBrush"] = selectedBrush;
                Application.Current.Resources["PropertyGridHoverBackgroundBrush"] = isTransparent ? new SolidColorBrush(fallbackSelected) { Opacity = 0.5 } : new SolidColorBrush(color) { Opacity = 0.5 };

                Application.Current.Resources["SecondaryAccentBrush"] = new SolidColorBrush(secondaryAccent);
                Application.Current.Resources["ItemPressedBrush"] = new SolidColorBrush(secondaryAccent) { Opacity = 0.5 };
            }

            // Save theme preference if settings service is available
            if (_settingsService != null)
            {
                _settingsService.CurrentTheme = themeName;
            }

            // Raise theme changed event
            OnThemeChanged(new ThemeChangedEventArgs(themeName));
        }

        /// <summary>
        /// Gets the name of the current theme applied to the application
        /// </summary>
        public string GetCurrentThemeFromResources()
        {
            var appResources = Application.Current.Resources.MergedDictionaries;

            // Check for an existing theme dictionary (either Dark.xaml or Light.xaml)
            var existingTheme = appResources.FirstOrDefault(d =>
                d.Source != null &&
                ((d.Source.OriginalString.Contains("Colors/Dark.xaml") || d.Source.OriginalString.Contains("Colors/Light.xaml"))));

            // If a theme is found, return the theme name (Dark or Light), else return default
            return existingTheme != null && existingTheme.Source.OriginalString.Contains("Dark.xaml")
                ? "Dark"
                : (existingTheme != null ? "Light" : "Dark");
        }

        /// <summary>
        /// Initializes the theme from saved settings
        /// </summary>
        public void InitializeTheme()
        {
            try
            {
                // Apply the theme from settings
                ApplyTheme(_currentTheme);

                // Log initialization
                System.Diagnostics.Debug.WriteLine($"Theme initialized: {_currentTheme}");
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
            ApplyTheme(_currentTheme == "Dark" ? "Light" : "Dark");
        }
    }
}