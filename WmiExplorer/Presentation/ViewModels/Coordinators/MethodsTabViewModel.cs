using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Methods tab. Manages method-related functionality
/// and UI operations for the methods list view and parameter display.
/// </summary>
public partial class MethodsTabViewModel : MessagingViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _helpText = "Select a class to view methods";

    [ObservableProperty]
    private string _methodFilterText = string.Empty;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    [ObservableProperty]
    private WmiMethod? _selectedMethod;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    private readonly IWmiService _wmiService;

    public MethodsTabViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           ISelectionService selectionService,
           IWmiService wmiService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Subscribe to unified selection changes
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

    /// <summary>
    /// Cleanup resources on disposal
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Handles the unified selection changed message
    /// </summary>
    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        var selectedObject = message.SelectionService.SelectedObject;

        switch (selectedObject)
        {
            // If a namespace is selected, update class selection
            case WmiNamespaceViewModel namespaceVm:
                if (namespaceVm.SelectedClass != SelectedClass)
                {
                    SelectedClass = namespaceVm.SelectedClass;
                }
                break;

            // If a class is selected, update the selected class
            case WmiClassViewModel classVm:
                if (classVm != SelectedClass)
                {
                    SelectedClass = classVm;
                }
                break;

            // If a method is selected, update the selected method
            case WmiMethod methodVm:
                if (methodVm != SelectedMethod)
                {
                    SelectedMethod = methodVm;
                }
                break;
        }
    }

    /// <summary>
    /// Called when the selected class changes to reset the selected method
    /// </summary>
    partial void OnSelectedClassChanged(WmiClassViewModel? oldValue, WmiClassViewModel? newValue)
    {
        // Reset selected method when class changes
        SelectedMethod = null;

        // Update help text based on class selection
        UpdateHelpText();

        // Update status bar
        if (newValue != null)
        {
            var totalMethodCount = newValue.Methods?.Count ?? 0;
            var staticMethodCount = newValue.StaticMethods?.Count ?? 0;

            if (totalMethodCount > 0)
            {
                PublishSuccessState($"Found {totalMethodCount} methods ({staticMethodCount} static) for class {newValue.ClassName}");
            }
            else
            {
                PublishWarningState($"No methods available for class {newValue.ClassName}");
            }
        }
    }

    /// <summary>
    /// Called when the selected method changes
    /// </summary>
    partial void OnSelectedMethodChanged(WmiMethod? oldValue, WmiMethod? newValue)
    {
        UpdateHelpText();
    }

    /// <summary>
    /// Updates the help text based on current selection state
    /// </summary>
    private void UpdateHelpText()
    {
        if (SelectedClass == null)
        {
            HelpText = "Select a class to view methods";
        }
        else if (SelectedClass.Methods?.Count == 0)
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