using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Exposes application settings as needed for direct binding in XAML to avoid creating proxy properties in every ViewModel.
/// </summary>
public partial class SettingsManager : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IWmiService _wmiService;

    public SettingsManager(ISettingsService settingsService, IWmiService wmiService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
    }

    public AutoUpdateSettings? AutoUpdateSettings
    {
        get => Get<AutoUpdateSettings>(nameof(AutoUpdateSettings));
        set => Set(nameof(AutoUpdateSettings), value);
    }

    public WmiClassEnumerationFlags ClassEnumerationFilter
    {
        get => Get<WmiClassEnumerationFlags>(nameof(ClassEnumerationFilter));
        set => Set(nameof(ClassEnumerationFilter), value);
    }

    public ConfigMgrSettings? ConfigMgrSettings
    {
        get => Get<ConfigMgrSettings>(nameof(ConfigMgrSettings));
        set => Set(nameof(ConfigMgrSettings), value);
    }

    public string? CurrentTheme
    {
        get => Get<string>(nameof(CurrentTheme));
        set => Set(nameof(CurrentTheme), value);
    }

    public LogLevel LogLevel
    {
        get => Get<LogLevel>(nameof(LogLevel));
        set
        {
            Set(nameof(LogLevel), value);

            // Sync Serilog's global minimum level
            Log.SetMinimumLevel(value);
        }
    }

    public MainWindowPosition? MainWindowPosition
    {
        get => Get<MainWindowPosition>(nameof(MainWindowPosition));
        set => Set(nameof(MainWindowPosition), value);
    }

    public WmiOperationMode OperationMode
    {
        get => Get<WmiOperationMode>(nameof(OperationMode));
        set
        {
            Set(nameof(OperationMode), value);
            _wmiService.OperationMode = value; // Keep WmiService in sync
        }
    }

    // Proxy properties for all ISettingsService settings
    public bool ShowSystemClasses
    {
        get => Get<bool>(nameof(ShowSystemClasses));
        set => Set(nameof(ShowSystemClasses), value);
    }

    // Generic proxy getter/setter for settings
    private T? Get<T>(string propertyName)
    {
        var prop = _settingsService.GetType().GetProperty(propertyName);
        return prop != null ? (T?)prop.GetValue(_settingsService) : default;
    }

    private void Set<T>(string propertyName, T value)
    {
        var prop = _settingsService.GetType().GetProperty(propertyName);
        if (prop != null && !Equals(prop.GetValue(_settingsService), value))
        {
            prop.SetValue(_settingsService, value);
            //OnPropertyChanged(propertyName);
        }
    }
}