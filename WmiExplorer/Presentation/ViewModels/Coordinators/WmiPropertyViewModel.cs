using WmiExplorer.Common.Base;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// ViewModel for WMI property management and display.
/// Handles property-related functionality and messaging.
/// </summary>
/// <summary>
/// ViewModel for WMI property management and display.
/// Handles property-related functionality and messaging.
/// </summary>

// Stub implementation. Future implementation will include:
// 1. Property to store list of class properties (using [ObservableProperty])
// 2. Logic to fetch and display class properties
// 3. Filtering and sorting capabilities
// 4. Commands using [RelayCommand] attributes

public partial class WmiPropertyViewModel : MessagingViewModel
{
    private readonly IWmiService _wmiService;

    public WmiPropertyViewModel(IMessengerService messengerService, IWmiService wmiService) : base(messengerService)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
    }
}