using WmiExplorer.Common.Base;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Instances tab. Manages instance-related functionality
/// and UI operations for the instances list view.
/// </summary>
public partial class InstancesTabViewModel : SelectionAwareViewModelBase
{
    public InstancesTabViewModel(
                    IMessengerService messengerService,
                    SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
    }
}