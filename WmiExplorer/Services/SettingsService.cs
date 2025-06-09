using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services;

public class SettingsService : ISettingsService
{
    // Events
    public event EventHandler<bool>? ShowSystemClassesChanged;

    public event EventHandler<string>? ThemeChanged;

    // Settings properties

    private WmiClassTypeFlags _classTypeFilter = WmiClassTypeFlags.None;

    private string _currentTheme = "Dark";
    private readonly string _filePath;
    private MainWindowPosition _mainWindowPosition = new MainWindowPosition();
    private readonly IMessagingService _messagingService;
    private bool _showSystemClasses = false;

    public SettingsService(IMessagingService messagingService)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));

        // Set up file path for settings
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WmiExplorer", "settings.json");

        // Load settings from file
        LoadSettings();

        Debug.WriteLine($"SettingsService initialized with file path: {_filePath}");
    }

    // ClassTypeFilter property with change notification
    public WmiClassTypeFlags ClassTypeFilter
    {
        get => _classTypeFilter;
        set
        {
            if (_classTypeFilter != value)
            {
                _classTypeFilter = value;
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

    // ShowSystemClasses property with change notification
    public bool ShowSystemClasses
    {
        get => _showSystemClasses;
        set
        {
            if (_showSystemClasses != value)
            {
                _showSystemClasses = value;
                ShowSystemClassesChanged?.Invoke(this, value);
                // Optionally publish a message if needed for decoupled updates
            }
        }
    }

    // Method to reload settings from file
    public void ReloadSettings()
    {
        // Store old values to detect changes
        var oldTheme = _currentTheme;

        // Load settings from file
        LoadSettings();

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
                ShowSystemClasses = _showSystemClasses,
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

                // Parse ShowSystemClasses
                if (settings.TryGetProperty("ShowSystemClasses", out var showSystemClasses))
                {
                    _showSystemClasses = showSystemClasses.GetBoolean();
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
}