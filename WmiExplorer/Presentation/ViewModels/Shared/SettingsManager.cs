using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
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

        // Relay property changed events from the underlying settings service
        if (_settingsService is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }
    }

    // Strongly-typed property forwarding for all settings
    public AutoUpdateSettings AutoUpdateSettings
    {
        get => _settingsService.AutoUpdateSettings;
        set => _settingsService.AutoUpdateSettings = value;
    }

    public WmiClassEnumerationFlags ClassEnumerationFilter
    {
        get => _settingsService.ClassEnumerationFilter;
        set => _settingsService.ClassEnumerationFilter = value;
    }

    public ConfigMgrSettings ConfigMgrSettings
    {
        get => _settingsService.ConfigMgrSettings;
        set => _settingsService.ConfigMgrSettings = value;
    }

    public LogLevel LogLevel
    {
        get => _settingsService.LogLevel;
        set
        {
            _settingsService.LogLevel = value;
            Log.SetMinimumLevel(value); // Keep Serilog's global minimum level in sync
        }
    }

    public MainWindowPosition MainWindowPosition
    {
        get => _settingsService.MainWindowPosition;
        set => _settingsService.MainWindowPosition = value;
    }

    public WmiOperationMode OperationMode
    {
        get => _settingsService.OperationMode;
        set
        {
            _settingsService.OperationMode = value;
            _wmiService.OperationMode = value; // Keep WmiService in sync
        }
    }

    public bool ShowSystemClasses
    {
        get => _settingsService.ShowSystemClasses;
        set => _settingsService.ShowSystemClasses = value;
    }
}