using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

public partial class ItemStatus : ObservableObject
{
    [ObservableProperty]
    private Exception? exception;

    [ObservableProperty]
    private LoadState loadState = LoadState.Unknown;

    [ObservableProperty]
    private string statusMessage = string.Empty;
}

public enum LoadState
{
    Unknown,
    Loading,
    Expanding, // For namespace expansion
    Success,
    PartialSuccess, // Expanded for Namespaces, Props/Methods loaded for Classes, lazy properties for Instances
    Warning,
    Failed
}