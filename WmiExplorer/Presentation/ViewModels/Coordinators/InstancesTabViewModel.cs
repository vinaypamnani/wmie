using WmiExplorer.Common.Base;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
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

    /// <summary>
    /// Gets the header text for the Instances tab with count
    /// </summary>
    public string TabHeader
    {
        get
        {
            var selectedClass = SelectionManager.GetSelectedClass();
            if (selectedClass?.ItemStatus.LoadState == LoadState.Success)
            {
                var count = selectedClass?.Instances?.Count ?? 0;
                return $"Instances [{count}]";
            }
            return "Instances";
        }
    }

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedClassChanged(WmiClassViewModel? selectedClass)
    {
        // Notify that TabHeader has changed
        OnPropertyChanged(nameof(TabHeader));
    }
}