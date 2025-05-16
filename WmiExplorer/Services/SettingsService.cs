using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _filePath;
        private readonly IMessagingService _messagingService;

        // Settings properties
        private WmiClassTypeFlags _classTypeFilter = WmiClassTypeFlags.None;

        private string _currentTheme = "Dark";
        private MainWindowPosition _mainWindowPosition = new MainWindowPosition();

        public SettingsService(IMessagingService messagingService)
        {
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));

            // Set up file path for settings
            _filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WmiExplorer", "settings.json");

            // Load settings from file
            LoadSettings();

            Debug.WriteLine($"SettingsService initialized with ClassTypeFilter: {_classTypeFilter}");
        }

        // Events
        public event EventHandler<WmiClassTypeFlags>? ClassTypeFilterChanged;

        public event EventHandler<string>? ThemeChanged;

        // ClassTypeFilter property with change notification
        public WmiClassTypeFlags ClassTypeFilter
        {
            get => _classTypeFilter;
            set
            {
                if (_classTypeFilter != value)
                {
                    _classTypeFilter = value;
                    ClassTypeFilterChanged?.Invoke(this, value);

                    // Also publish a message for any subscribers
                    _messagingService.Publish(new ClassTypeFilterChangedMessage(value));
                }
            }
        }

        // CurrentTheme property with change notification
        public string CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ThemeChanged?.Invoke(this, value);
                    Debug.WriteLine($"CurrentTheme changed to: {value}");
                }
            }
        }

        // Main window position property
        public MainWindowPosition MainWindowPosition
        {
            get => _mainWindowPosition;
            set
            {
                _mainWindowPosition = value;
                // Note: We do not automatically save settings here anymore
                // Settings will be saved explicitly on app exit
            }
        }

        // Method to load settings from file
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    Debug.WriteLine($"Loaded settings from: {_filePath}");

                    var settings = JsonSerializer.Deserialize<JsonElement>(json);

                    // Parse values from JSON with type safety
                    if (settings.TryGetProperty("ClassTypeFilter", out var classTypeFilter))
                    {
                        _classTypeFilter = (WmiClassTypeFlags)classTypeFilter.GetInt32();
                    }

                    if (settings.TryGetProperty("CurrentTheme", out var currentTheme))
                    {
                        _currentTheme = currentTheme.GetString() ?? "Dark";
                    }

                    // Parse MainWindowPosition
                    if (settings.TryGetProperty("MainWindowPosition", out var mainWindowPosition))
                    {
                        try
                        {
                            _mainWindowPosition = JsonSerializer.Deserialize<MainWindowPosition>(mainWindowPosition.GetRawText())
                                ?? new MainWindowPosition();                            
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error deserializing MainWindowPosition: {ex.Message}");
                            // Use default values on error
                        }
                    }
                    else
                    {
                        // Try to migrate from old window position settings for compatibility with existing settings files
                        try
                        {
                            if (settings.TryGetProperty("WindowTop", out var windowTop) &&
                                settings.TryGetProperty("WindowLeft", out var windowLeft) &&
                                settings.TryGetProperty("WindowWidth", out var windowWidth) &&
                                settings.TryGetProperty("WindowHeight", out var windowHeight))
                            {
                                _mainWindowPosition = new MainWindowPosition
                                {
                                    Top = windowTop.GetDouble(),
                                    Left = windowLeft.GetDouble(),
                                    Width = windowWidth.GetDouble(),
                                    Height = windowHeight.GetDouble(),
                                    IsWindowMaximized = false // Default to false for backward compatibility
                                };

                                Debug.WriteLine("Migrated from old window position settings");

                                // Save settings to update the format
                                SaveSettings();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error migrating window position settings: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Use default values if file doesn't exist
                    Debug.WriteLine("Settings file not found, using defaults");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Keep default values on error
            }
        }

        // Method to reload settings from file
        public void ReloadSettings()
        {
            // Store old values to detect changes
            var oldClassTypeFilter = _classTypeFilter;
            var oldTheme = _currentTheme;

            // Load settings from file
            LoadSettings();

            // Notify about any changes
            if (oldClassTypeFilter != _classTypeFilter)
            {
                ClassTypeFilterChanged?.Invoke(this, _classTypeFilter);
                _messagingService.Publish(new ClassTypeFilterChangedMessage(_classTypeFilter));
            }

            if (oldTheme != _currentTheme)
            {
                ThemeChanged?.Invoke(this, _currentTheme);
            }
        }

        // Method to save settings to file
        public void SaveSettings()
        {
            try
            {
                var settingsData = new
                {
                    ClassTypeFilter = _classTypeFilter,
                    CurrentTheme = _currentTheme,
                    MainWindowPosition = _mainWindowPosition
                };

                // Create directory if it doesn't exist
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Debug.WriteLine($"Created settings directory: {dir}");
                }

                // Serialize and save settings
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settingsData, options);
                File.WriteAllText(_filePath, json);

                Debug.WriteLine($"Saved settings to: {_filePath}");
                Debug.WriteLine($"Current theme saved as: {_currentTheme}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}