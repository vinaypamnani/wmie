using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Methods tab. Manages method-related functionality
/// and UI operations for the methods list view and parameter display.
/// </summary>
public partial class MethodsTabViewModel : SelectionAwareViewModelBase
{
    [ObservableProperty]
    private string _helpText = "Select a class to view methods";

    private FilterHelper<WmiMethod>? _methodFilterHelper;

    [ObservableProperty]
    private string _methodFilterText = string.Empty;

    [ObservableProperty]
    private WmiMethod? _selectedMethod;

    [ObservableProperty]
    private WmiParameter? _selectedMethodParameter;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public MethodsTabViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

    /// <summary>
    /// Gets the filtered view of methods for the selected class.
    /// </summary>
    public ICollectionView? FilteredMethodsView => _methodFilterHelper?.CollectionView;

    /// <summary>
    /// Cleanup resources on disposal
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _methodFilterHelper?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedClassChanged(WmiClassViewModel? selectedClass)
    {
        // Update filtered methods when class selection changes
        UpdateFilteredMethods(selectedClass);
    }

    /// <summary>
    /// Filter predicate for methods based on name and description
    /// </summary>
    private bool MethodFilterPredicate(WmiMethod method, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return method.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               method.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Called when the MethodFilterText changes
    /// </summary>
    partial void OnMethodFilterTextChanged(string value)
    {
        if (_methodFilterHelper != null)
        {
            _methodFilterHelper.FilterText = value;
        }
    }

    /// <summary>
    /// Called when the selected method changes
    /// </summary>
    partial void OnSelectedMethodChanged(WmiMethod? oldValue, WmiMethod? newValue)
    {
        // Clear selected parameter when method changes since parameters are method-specific
        SelectedMethodParameter = null;

        UpdateHelpText();
    }

    /// <summary>
    /// Update the filtered methods based on the provided class selection
    /// </summary>
    private void UpdateFilteredMethods(WmiClassViewModel? selectedClass)
    {
        // Reset selected method when class changes
        SelectedMethod = null;

        // Dispose of the old filter helper
        _methodFilterHelper?.Dispose();
        _methodFilterHelper = null;

        // Create new filter helper if we have methods
        if (selectedClass?.Methods != null)
        {
            _methodFilterHelper = new FilterHelper<WmiMethod>(
                selectedClass.Methods,
                MethodFilterPredicate
            );

            // Apply current filter text if any
            if (!string.IsNullOrWhiteSpace(MethodFilterText))
            {
                _methodFilterHelper.FilterText = MethodFilterText;
            }
        }

        // Notify UI that FilteredMethodsView has changed
        OnPropertyChanged(nameof(FilteredMethodsView));

        // Update help text based on class selection
        UpdateHelpText();
    }

    /// <summary>
    /// Updates the help text based on current selection state
    /// </summary>
    private void UpdateHelpText()
    {
        if (SelectionManager.SelectedClass == null)
        {
            HelpText = "Select a class to view methods";
        }
        else if (SelectionManager.SelectedClass.Methods?.Count == 0)
        {
            HelpText = "No methods available for the selected class";
        }
        else if (SelectedMethod == null)
        {
            HelpText = "Select a method to view its parameters and execution details";
        }
        else if (SelectedMethod.IsStatic)
        {
            HelpText = "Static Method - Right click the method or the class to execute this method";
        }
        else
        {
            HelpText = "Non-Static Method - Right click an instance of this class to execute this method";
        }
    }
}