using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

public partial class ItemStatus : ObservableObject
{
    [ObservableProperty]
    private Exception? exception;

    [ObservableProperty]
    private LoadState loadState = LoadState.Unknown;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Maps LoadState values to their equivalent AppState values for comparison purposes
    /// </summary>
    public static AppState MapLoadStateToAppState(LoadState loadState)
    {
        return loadState switch
        {
            LoadState.Unknown => AppState.Unknown,
            LoadState.Loading => AppState.Busy,
            LoadState.Expanding => AppState.Busy,
            LoadState.Success => AppState.Success,
            LoadState.PartialSuccess => AppState.PartialSuccess,
            LoadState.Warning => AppState.Warning,
            LoadState.Error => AppState.Error,
            _ => AppState.Unknown
        };
    }
}

public enum LoadState
{
    Unknown,
    Loading,
    Expanding, // For namespace expansion
    Success,
    PartialSuccess, // Expanded for Namespaces, Props/Methods loaded for Classes, lazy properties for Instances
    Warning,
    Error
}