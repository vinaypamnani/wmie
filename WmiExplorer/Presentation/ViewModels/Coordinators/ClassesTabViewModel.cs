using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Classes tab. Manages the collection of classes
/// and related UI operations for the classes list view.
/// </summary>
public partial class ClassesTabViewModel : SelectionAwareViewModelBase
{
    [ObservableProperty]
    private string _autoQueryText = string.Empty;

    private readonly InstancesTabViewModel _instancesTabViewModel;
    private readonly MethodsTabViewModel _methodsTabViewModel;
    private readonly PropertiesTabViewModel _propertiesTabViewModel;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly SettingsManager _settingsManager;

    public ClassesTabViewModel(
                 IMessengerService messengerService,
                 SelectionManager selectionManager,
                 SettingsManager settingsManager,
                 InstancesTabViewModel instancesTabViewModel,
                 MethodsTabViewModel methodsTabViewModel,
                 PropertiesTabViewModel propertiesTabViewModel) : base(messengerService, selectionManager)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _instancesTabViewModel = instancesTabViewModel ?? throw new ArgumentNullException(nameof(instancesTabViewModel));
        _methodsTabViewModel = methodsTabViewModel ?? throw new ArgumentNullException(nameof(methodsTabViewModel));
        _propertiesTabViewModel = propertiesTabViewModel ?? throw new ArgumentNullException(nameof(propertiesTabViewModel));

        // Subscribe to messages
        StrongSubscribe<TabCountChangedMessage>(message => UpdateTabHeaders());
    }

    /// <summary>
    /// Gets the header text for the Instances tab with count
    /// </summary>
    public string InstancesTabHeader
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
    /// Gets the InstancesTabViewModel
    /// </summary>
    public InstancesTabViewModel InstancesTabViewModel => _instancesTabViewModel;

    /// <summary>
    /// Gets the header text for the Methods tab with count
    /// </summary>
    public string MethodsTabHeader
    {
        get
        {
            var selectedClass = SelectionManager.GetSelectedClass();
            if (selectedClass?.Methods is not null)
            {
                var count = selectedClass?.Methods?.Count ?? 0;
                return $"Methods [{count}]";
            }
            return "Methods";
        }
    }

    /// <summary>
    /// Gets the MethodsTabViewModel
    /// </summary>
    public MethodsTabViewModel MethodsTabViewModel => _methodsTabViewModel;

    /// <summary>
    /// Gets the header text for the Properties tab with count
    /// </summary>
    public string PropertiesTabHeader
    {
        get
        {
            var selectedClass = SelectionManager.GetSelectedClass();
            if (selectedClass?.Properties is not null)
            {
                // Count the properties in the selected class
                var count = selectedClass?.Properties?.Count ?? 0;
                return $"Properties [{count}]";
            }
            return "Properties";
        }
    }

    /// <summary>
    /// Gets the PropertiesTabViewModel
    /// </summary>
    public PropertiesTabViewModel PropertiesTabViewModel => _propertiesTabViewModel;

    public SettingsManager SettingsManager => _settingsManager;

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedClassChanged(WmiClassViewModel? selectedClass)
    {
        UpdateTabHeaders();
        UpdateAutoQueryText(selectedClass!);
    }

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedInstanceChanged(WmiInstanceViewModel? selectedInstance)
    {
        UpdateAutoQueryText(selectedInstance!);
    }

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        UpdateTabHeaders();
    }

    /// <summary>
    /// Executes the auto-generated query by requesting a tab switch, setting the query, and executing it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteAutoQueryCanExecute))]
    private async Task ExecuteAutoQuery()
    {
        if (string.IsNullOrWhiteSpace(AutoQueryText))
            return;

        try
        {
            // Define the index for the Query tab (update if needed)
            const int QueryTabIndex = 2;

            // Request MainViewModel to switch to the Query tab via message
            _messengerService.Send(new SwitchMainTabMessage(QueryTabIndex));

            // Get the QueryTabViewModel for the currently selected namespace
            var selectedNamespace = SelectionManager.SelectedNamespace;
            if (selectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }

            var queryTabViewModel = selectedNamespace.QueryTabViewModel;
            if (queryTabViewModel == null)
            {
                PublishErrorState("Query tab is not available for the selected namespace.");
                return;
            }

            // Set the query text
            queryTabViewModel.QueryText = AutoQueryText;

            // Execute the query if possible
            if (queryTabViewModel.ExecuteQueryCommand.CanExecute(null))
            {
                // Await the async command execution if possible
                var executionTask = queryTabViewModel.ExecuteQueryCommand.ExecuteAsync(null);
                if (executionTask != null)
                {
                    await executionTask;
                }
            }
            else
            {
                PublishWarningState("Query cannot be executed at this time. ExecuteQueryCommand.CanExecute() returned false.");
            }
        }
        catch (Exception ex)
        {
            PublishErrorState("Failed to execute auto-query.", ex);
        }
    }

    /// <summary>
    /// Determines if the auto query command can execute
    /// </summary>
    private bool ExecuteAutoQueryCanExecute() => !string.IsNullOrWhiteSpace(AutoQueryText);

    partial void OnAutoQueryTextChanged(string value)
    {
        // Notify that the CanExecute state of ExecuteAutoQueryCommand may have changed
        ExecuteAutoQueryCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Set selections when the selected tab index changes
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        switch (value)
        {
            case 1:
                // Properties Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(PropertiesTabViewModel?.SelectedProperty);
                break;
            case 2:
                // Methods Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(MethodsTabViewModel?.SelectedMethod);
                break;
            default:
                // Instances tab
                if (SelectionManager.GetSelectedInstance() != null)
                {
                    SelectionManager.SetSelectedObject(SelectionManager.GetSelectedInstance(), updatePropertyGrid: true);
                }
                break;
        }
    }

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    [RelayCommand(CanExecute = nameof(ReloadClassesCanExecute))]
    private void ReloadClasses()
    {
        SelectionManager.SelectedNamespace?.LoadClassesCommand.Execute(null);
    }

    /// <summary>
    /// Determines if the reload classes command can execute
    /// </summary>
    private bool ReloadClassesCanExecute() => SelectionManager.SelectedNamespace != null && SelectionManager.SelectedNamespace.LoadClassesCommand.CanExecute(null);

    /// <summary>
    /// Updates the auto-generated WQL query text based on the selected class or instance
    /// </summary>
    private void UpdateAutoQueryText(object selectedObject)
    {
        var selectedClassName = SelectionManager.GetSelectedClass()?.ClassName ?? string.Empty;

        if (selectedObject is WmiInstanceViewModel selectedInstance)
        {
            // Create query based on the instance
            string className = selectedInstance.WmiInstance.ClassPath.ClassName
                               ?? selectedClassName
                               ?? string.Empty;
            string relativePath = selectedInstance.InstanceName.Replace($"{className}.", string.Empty);
            relativePath = relativePath.Replace(",", " AND ");
            if (!string.IsNullOrEmpty(relativePath))
            {
                // For instances, use a direct reference query
                AutoQueryText = $"SELECT * FROM {selectedClassName} WHERE {relativePath}";
            }
            else if (selectedClassName != null)
            {
                // Fallback to a class query
                AutoQueryText = $"SELECT * FROM {selectedClassName}";
            }
            else
            {
                AutoQueryText = string.Empty;
            }
        }
        else if (selectedObject is WmiClassViewModel)
        {
            // Create query based on just the class
            AutoQueryText = $"SELECT * FROM {selectedClassName}";
        }
        else
        {
            AutoQueryText = string.Empty;
        }
    }

    /// <summary>
    /// Updates tab header properties to trigger UI refresh
    /// </summary>
    private void UpdateTabHeaders()
    {
        OnPropertyChanged(nameof(InstancesTabHeader));
        OnPropertyChanged(nameof(PropertiesTabHeader));
        OnPropertyChanged(nameof(MethodsTabHeader));
    }
}