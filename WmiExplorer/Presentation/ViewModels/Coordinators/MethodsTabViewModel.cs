using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
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

    [ObservableProperty]
    private TabStatus _tabStatus;

    public MethodsTabViewModel(
           IMessengerService messengerService,
           SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        // Initialize tab status with messenger service
        _tabStatus = new TabStatus("WMI Methods");
    }

    /// <summary>
    /// Gets the filtered view of methods for the selected class.
    /// </summary>
    public ICollectionView? FilteredMethodsView => _methodFilterHelper?.CollectionView;

    /// <summary>
    /// Gets the header text for the Methods tab with count
    /// </summary>
    public string TabHeader
    {
        get
        {
            var filteredCount = FilteredMethodsView?.Cast<object>().Count();
            if (filteredCount.HasValue)
            {
                return $"Methods [{filteredCount.Value}]";
            }
            return "Methods";
        }
    }

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

        // Notify that TabHeader has changed
        OnPropertyChanged(nameof(TabHeader));
    }

    /// <summary>
    /// Command to generate PowerShell script for the selected method.
    /// </summary>
    [RelayCommand(CanExecute = nameof(GenerateScriptCanExecute))]
    private void GenerateScript()
    {
        try
        {
            if (SelectedMethod == null || SelectionManager.SelectedNamespace?.SelectedClass == null)
                return;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            var managementScope = SelectionManager.SelectedNamespace.ManagementScope;

            // Show the GenerateScriptDialog
            WmiExplorer.Presentation.Views.Dialogs.GenerateScriptDialog.ShowDialog(
                mainWindow,
                SelectedMethod,
                managementScope);

            Log.Information("Generated PowerShell script for method: {MethodName}", SelectedMethod.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating PowerShell script for method: {MethodName}", SelectedMethod?.Name ?? "Unknown");
            // Note: We don't have access to PublishErrorState here, so we'll just log the error
        }
    }

    /// <summary>
    /// Determines whether the GenerateScript command can execute.
    /// </summary>
    private bool GenerateScriptCanExecute()
    {
        return SelectedMethod != null && SelectionManager.SelectedNamespace?.SelectedClass != null;
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
            // Notify that TabHeader has changed since the filtered count may have changed
            OnPropertyChanged(nameof(TabHeader));
        }
    }

    /// <summary>
    /// Called when the selected method changes
    /// </summary>
    partial void OnSelectedMethodChanged(WmiMethod? value)
    {
        // Clear selected parameter when method changes since parameters are method-specific
        SelectedMethodParameter = null;

        UpdateHelpText();
    }

    partial void OnSelectedMethodParameterChanged(WmiParameter? value)
    {
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

        if (selectedClass != null)
        {
            // CRITICAL: Ensure methods are loaded before accessing them.
            // When SelectedClass is set via ListView binding, OnSelectedClassChanged may be called
            // before OnIsSelectedChanged completes loading properties/methods.
            selectedClass.EnsurePropertiesAndMethodsLoaded();

            // Methods is guaranteed to be non-null after EnsurePropertiesAndMethodsLoaded()
            // (it will be an empty collection if there are no methods, but not null)
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
        var selectedClass = SelectionManager.GetSelectedClass();
        if (selectedClass == null)
        {
            HelpText = "Select a class to view methods";
        }
        else if (selectedClass?.Methods?.Count == 0)
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