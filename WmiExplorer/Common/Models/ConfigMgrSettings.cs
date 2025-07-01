using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Encapsulates Configuration Manager related settings.
/// </summary>
public partial class ConfigMgrSettings : ObservableObject
{
    [ObservableProperty]
    private bool _configMgrModeEnabled = true;

    [ObservableProperty]
    private bool _includeCollectionClasses = false;

    [ObservableProperty]
    private bool _includeInventoryClasses = false;
}