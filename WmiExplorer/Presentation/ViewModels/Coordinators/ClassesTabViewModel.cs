using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
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
        StrongSubscribe<InstancesFilteredMessage>(HandleInstancesFilteredMessage);
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
            if (selectedClass?.LoadState == InstanceLoadState.Success)
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
        UpdateStatusBar();
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
    /// Command to execute the auto-generated query
    /// </summary>
    [RelayCommand(CanExecute = nameof(ExecuteAutoQueryCanExecute))]
    private void ExecuteAutoQuery()
    {
        if (string.IsNullOrWhiteSpace(AutoQueryText))
            return;

        // Log the query execution
        PublishWarningState($"[Not implemented] Executing query: {AutoQueryText}");
    }

    /// <summary>
    /// Determines if the auto query command can execute
    /// </summary>
    private bool ExecuteAutoQueryCanExecute() => !string.IsNullOrWhiteSpace(AutoQueryText);

    /// <summary>
    /// Handles the instances filtered message
    /// </summary>
    private void HandleInstancesFilteredMessage(InstancesFilteredMessage message)
    {
        if (message?.ClassViewModel != null && message.ClassViewModel == SelectionManager.GetSelectedClass())
        {
            UpdateStatusBar();
            UpdateTabHeaders();
        }
    }

    partial void OnAutoQueryTextChanged(string value)
    {
        // Notify that the CanExecute state of ExecuteAutoQueryCommand may have changed
        ExecuteAutoQueryCommand.NotifyCanExecuteChanged();
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
    /// Updates the status bar message based on the selected namespace or class load states
    /// </summary>
    private void UpdateStatusBar()
    {
        // If no namespace is selected, do nothing
        if (SelectionManager.SelectedNamespace == null || SelectionManager.SelectedNamespace.NamespaceLoadState != NamespaceLoadState.Success)
            return;

        // If a class is selected, show status based on class load state
        var selectedClass = SelectionManager.GetSelectedClass();
        if (selectedClass != null)
        {
            switch (selectedClass.LoadState)
            {
                case InstanceLoadState.Unknown:
                    PublishSuccessState($"Selected class {selectedClass.ClassName}. Double-click the class to load instances.");
                    break;
                case InstanceLoadState.Loading:
                    PublishBusyState($"Loading instances for class {selectedClass.ClassName}...");
                    break;
                case InstanceLoadState.Warning:
                    PublishWarningState($"Showing partial results for class {selectedClass.ClassName}.");
                    break;
                case InstanceLoadState.Failed:
                    PublishErrorState($"Failed to load instances for class {selectedClass.ClassName}. Double-click the class to try again.", selectedClass.LoadException);
                    break;
                case InstanceLoadState.Success:
                    var count = selectedClass.InstancesView.Cast<object>().Count();
                    var total = selectedClass.Instances.Count;
                    if (count < total)
                        PublishSuccessState($"Showing {count} of {total} instances for class {selectedClass.ClassName}.");
                    else
                        PublishSuccessState($"Showing {count} instances for class {selectedClass.ClassName}.");
                    break;
            }
            return;
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