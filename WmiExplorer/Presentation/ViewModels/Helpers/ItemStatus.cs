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
    Expanded,  // For when a namespace has been expanded
    Success,
    PartialSuccess, // For lazy properties or partial data loads
    Warning,
    Failed
}