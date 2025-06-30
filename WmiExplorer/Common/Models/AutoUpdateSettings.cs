using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.Common.Models;

/// <summary>
/// Encapsulates auto-update related settings.
/// </summary>
public partial class AutoUpdateSettings : ObservableObject
{
    [ObservableProperty]
    private bool _checkOnStartup = true;

    [ObservableProperty]
    private int _intervalDays = 7;

    [ObservableProperty]
    private DateTime? _lastCheckTime = null;
}