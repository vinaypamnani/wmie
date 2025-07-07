using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;

namespace WmiExplorer.Services;

/// <summary>
/// Attribute to mark properties as settings that should be persisted
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingAttribute : Attribute
{
    public SettingAttribute(object? defaultValue = null)
    {
        DefaultValue = defaultValue;
    }

    public object? DefaultValue { get; set; }
    public string? Key { get; set; }
}

/// <summary>
/// Advanced settings service with attribute-based configuration and validation
/// </summary>
public class SettingsService : ISettingsService, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private const int SaveDebounceMilliseconds = 500;

    private readonly string _filePath;
    private readonly IMessengerService _messengerService;

    // Debounce timer for saving settings
    private readonly object _saveLock = new();

    private System.Timers.Timer? _saveTimer;
    private readonly Dictionary<string, PropertyInfo> _settingsProperties;
    private readonly Dictionary<string, object?> _values = new();

    public SettingsService(IMessengerService messengerService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));

        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WmiExplorer", "settings.json");

        _settingsProperties = GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<SettingAttribute>() != null)
            .OrderBy(p => p.GetCustomAttribute<SettingAttribute>()?.Key ?? p.Name)
            .ToDictionary(p => GetSettingKey(p), p => p);

        InitializeDefaults();
        LoadSettings();

        Log.Information("Settings initialized with {SettingCount} settings. Settings file: {FilePath}", _settingsProperties.Count, _filePath);
    }

    [Setting]
    public AutoUpdateSettings AutoUpdateSettings
    {
        get => GetOrCreateObjectSetting<AutoUpdateSettings>();
        set
        {
            var current = GetValue<AutoUpdateSettings>();
            SubscribeToNestedSettings(current, value);
            SetValue(value);
        }
    }

    [Setting(WmiClassEnumerationFlags.None)]
    public WmiClassEnumerationFlags ClassEnumerationFilter
    {
        get => GetValue<WmiClassEnumerationFlags>();
        set => SetValue(value);
    }

    [Setting]
    public ConfigMgrSettings ConfigMgrSettings
    {
        get => GetOrCreateObjectSetting<ConfigMgrSettings>();
        set
        {
            var current = GetValue<ConfigMgrSettings>();
            SubscribeToNestedSettings(current, value);
            SetValue(value);
        }
    }

    [Setting("Dark")]
    public string CurrentTheme
    {
        get => GetValue<string>();
        set
        {
            // Simple validation - only allow Dark or Light themes
            if (value != "Dark" && value != "Light")
            {
                Log.Warning("Invalid theme '{Theme}' - using default 'Dark'", value);
                value = "Dark";
            }
            SetValue(value);
        }
    }

    [Setting(LogLevel.Information)]
    public LogLevel LogLevel
    {
        get => GetValue<LogLevel>();
        set => SetValue(value);
    }

    [Setting]
    public MainWindowPosition MainWindowPosition
    {
        get => GetOrCreateObjectSetting<MainWindowPosition>();
        set
        {
            var current = GetValue<MainWindowPosition>();
            SubscribeToNestedSettings(current, value);
            SetValue(value);
        }
    }

    [Setting(WmiOperationMode.Asynchronous)]
    public WmiOperationMode OperationMode
    {
        get => GetValue<WmiOperationMode>();
        set => SetValue(value);
    }

    [Setting(false)]
    public bool ShowSystemClasses
    {
        get => GetValue<bool>();
        set => SetValue(value);
    }

    public void ReloadSettings()
    {
        var oldValues = new Dictionary<string, object?>(_values);
        LoadSettings();

        // Check for changes and send messages
        foreach (var kvp in _settingsProperties)
        {
            var key = kvp.Key;
            var oldValue = oldValues.GetValueOrDefault(key);
            var newValue = _values.GetValueOrDefault(key);

            if (!Equals(oldValue, newValue))
            {
                _messengerService.Send(new SettingChangedMessage(key, oldValue, newValue));
            }
        }

        Log.Information("Settings reloaded from {FilePath}", _filePath);
    }

    public void ResetToDefaults()
    {
        InitializeDefaults();
        SaveSettings();
        Log.Information("All settings reset to defaults");
    }

    /// <summary>
    /// Debounced save method. Schedules a save operation after a short delay.
    /// If called again within the delay, the timer is reset.
    /// </summary>
    public void SaveSettings()
    {
        lock (_saveLock)
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Timers.Timer(SaveDebounceMilliseconds)
                {
                    AutoReset = false
                };
                _saveTimer.Elapsed += (s, e) => PerformSaveSettings();
            }
            else
            {
                _saveTimer.Stop();
            }
            _saveTimer.Start();
        }
    }

    private static object? DeserializeValue(JsonElement jsonValue, Type targetType)
    {
        try
        {
            if (targetType == typeof(string))
                return jsonValue.GetString();
            if (targetType == typeof(bool))
                return jsonValue.GetBoolean();
            if (targetType == typeof(int))
                return jsonValue.GetInt32();
            if (targetType == typeof(double))
                return jsonValue.GetDouble();
            if (targetType.IsEnum)
                return Enum.ToObject(targetType, jsonValue.GetInt32());

            return JsonSerializer.Deserialize(jsonValue.GetRawText(), targetType);
        }
        catch
        {
            return null;
        }
    }

    // Helper to get or create, store, and subscribe to a settings object
    private T GetOrCreateObjectSetting<T>([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    where T : class, INotifyPropertyChanged, new()
    {
        var value = GetValue<T>(propertyName);
        if (value == null)
        {
            value = new T();
            SetValue(value, propertyName);
        }
        SubscribeToNestedSettings(null, value);
        return value;
    }

    private static string GetSettingKey(PropertyInfo property)
    {
        var attribute = property.GetCustomAttribute<SettingAttribute>();
        return attribute?.Key ?? property.Name;
    }

    private T GetValue<T>([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return default(T)!;

        if (_values.TryGetValue(propertyName, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            // Try to convert if types don't match exactly
            try
            {
                return (T)Convert.ChangeType(value, typeof(T))!;
            }
            catch
            {
                // Fall back to default
            }
        }

        return default(T)!;
    }

    private void InitializeDefaults()
    {
        foreach (var kvp in _settingsProperties)
        {
            var attribute = kvp.Value.GetCustomAttribute<SettingAttribute>();
            if (attribute?.DefaultValue != null)
            {
                _values[kvp.Key] = attribute.DefaultValue;
            }
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Log.Information("Settings file not found, using defaults");
                return;
            }

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<JsonElement>(json);

            foreach (var kvp in _settingsProperties)
            {
                var key = kvp.Key;
                var propInfo = kvp.Value;

                if (settings.TryGetProperty(key, out var jsonValue))
                {
                    try
                    {
                        var value = DeserializeValue(jsonValue, propInfo.PropertyType);
                        _values[key] = value;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error deserializing setting {Key}", key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading settings");
        }
    }

    // Generic handler for nested ObservableObject settings
    private void NestedSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var settingsType = sender?.GetType().Name ?? "UnknownSettings";
        var propertyName = e.PropertyName ?? string.Empty;
        object? value = null;
        if (sender != null && !string.IsNullOrEmpty(propertyName))
        {
            var prop = sender.GetType().GetProperty(propertyName);
            if (prop != null)
                value = prop.GetValue(sender);
        }

        if (propertyName.EndsWith("GridLength")) return;

        Log.Information("Setting {SettingsType}.{PropertyName} changed to {Value}", settingsType, propertyName, value ?? "<null>");
        SaveSettings();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Actually writes the settings to disk. Called by the debounce timer.
    /// </summary>
    private void PerformSaveSettings()
    {
        lock (_saveLock)
        {
            try
            {
                var settingsData = new Dictionary<string, object?>();

                foreach (var kvp in _settingsProperties)
                {
                    var key = kvp.Key;
                    if (_values.TryGetValue(key, out var value))
                    {
                        settingsData[key] = value;
                    }
                }

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Log.Information("Created settings directory: {Directory}", dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settingsData, options);
                File.WriteAllText(_filePath, json);

                Log.Debug("Saved {SettingCount} settings to {FilePath}", settingsData.Count, _filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving settings");
            }
        }
    }

    private void SetValue<T>(T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return;

        var currentValue = GetValue<T>(propertyName);

        // NOTE: This equality check only works for top-level settings properties.
        // For nested settings (e.g., ObservableObject-based), property changes are handled via change notification.
        if (EqualityComparer<T>.Default.Equals(currentValue, value))
            return;

        // Store old value for message
        var oldValue = currentValue;
        _values[propertyName] = value;

        // Notify UI binding system
        OnPropertyChanged(propertyName);

        // Send generic setting change message
        _messengerService.Send(new SettingChangedMessage<T>(propertyName, oldValue, value));
        Log.Information("Setting {PropertyName} changed to {Value}", propertyName, value?.ToString() ?? "null");

        // Auto-save settings after any change
        SaveSettings();
    }

    // Helper to subscribe/unsubscribe to nested ObservableObject settings
    private void SubscribeToNestedSettings(INotifyPropertyChanged? oldValue, INotifyPropertyChanged? newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= NestedSettings_PropertyChanged;
        if (newValue != null)
            newValue.PropertyChanged -= NestedSettings_PropertyChanged; // Ensure no duplicate subscription
        if (newValue != null)
            newValue.PropertyChanged += NestedSettings_PropertyChanged;
    }
}