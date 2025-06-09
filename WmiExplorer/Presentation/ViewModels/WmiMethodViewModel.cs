using CommunityToolkit.Mvvm.Messaging;
using WmiExplorer.Common.Base;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// ViewModel for WMI method management and execution.
/// Handles method-related functionality and messaging.
/// </summary>
/// <summary>
/// ViewModel for WMI method management and execution.
/// Handles method-related functionality and messaging.
/// </summary>

// Stub implementation. Future implementation will include:
// 1. Property to store list of class methods (using [ObservableProperty])
// 2. Logic to fetch and display class methods
// 3. Method invocation capabilities (using [RelayCommand])
// 4. Parameter handling for method execution

public partial class WmiMethodViewModel : MessagingViewModel
{
    private readonly IWmiService _wmiService;

    public WmiMethodViewModel(IMessenger messenger, IWmiService wmiService) : base(messenger)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
    }
}