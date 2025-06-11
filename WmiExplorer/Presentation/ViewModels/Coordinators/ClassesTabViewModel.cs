using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Classes tab. Manages the collection of classes
/// and related UI operations for the classes list view.
/// </summary>
public partial class ClassesTabViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;

    [ObservableProperty]
    private string _autoQueryText = string.Empty;

    private readonly ICacheService _cacheService;
    private readonly CancellationTokenSource _cts = new();
    private readonly InstancesTabViewModel _instancesTabViewModel;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadClassesCommand))]
    private WmiNamespaceViewModel? _selectedNamespace;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _showSystemClasses;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    private readonly IWmiService _wmiService;

    public ClassesTabViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           IWmiService wmiService,
           IApplicationService applicationService,
           ICacheService cacheService,
           ISelectionService selectionService,
           InstancesTabViewModel instancesTabViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        _instancesTabViewModel = instancesTabViewModel ?? throw new ArgumentNullException(nameof(instancesTabViewModel));

        // Subscribe to messages
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);
        StrongSubscribe<ClassesLoadedMessage>(HandleClassesLoadedMessage);
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        // Initialize properties from settings
        _showSystemClasses = _settingsService.ShowSystemClasses;

        // Subscribe to ShowSystemClassesChanged event
        _settingsService.ShowSystemClassesChanged += (s, v) =>
        {
            ShowSystemClasses = v;
        };
    }

    /// <summary>
    /// Gets the InstancesTabViewModel
    /// </summary>
    public InstancesTabViewModel InstancesTabViewModel => _instancesTabViewModel;

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
    /// Handles the ClassesFilteredMessage
    /// </summary>
    private void HandleClassesFilteredMessage(ClassesFilteredMessage message)
    {
        // Update UI or perform other actions when classes are filtered
        if (message.NamespaceViewModel == SelectedNamespace)
        {
            OnPropertyChanged(nameof(SelectedNamespace));
        }
    }

    /// <summary>
    /// Handles the ClassesLoadedMessage
    /// </summary>
    private void HandleClassesLoadedMessage(ClassesLoadedMessage message)
    {
        // Update UI or perform other actions when classes are loaded
        OnPropertyChanged(nameof(SelectedNamespace));
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
            case WmiNamespaceViewModel namespaceVm:
                if (namespaceVm != SelectedNamespace)
                {
                    SelectedNamespace = namespaceVm;
                }
                break;

            case WmiClassViewModel classVm:
                SelectedClass = classVm;
                UpdateAutoQueryText(classVm);
                break;

            case WmiInstanceViewModel instanceVm:
                // Update auto-query for instance selections
                UpdateAutoQueryText(instanceVm);
                break;
        }
    }

    partial void OnAutoQueryTextChanged(string value)
    {
        // Notify that the CanExecute state of ExecuteAutoQueryCommand may have changed
        ExecuteAutoQueryCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowSystemClassesChanged(bool value)
    {
        _settingsService.ShowSystemClasses = value;
    }

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    [RelayCommand(CanExecute = nameof(ReloadClassesCanExecute))]
    private void ReloadClasses()
    {
        SelectedNamespace?.LoadClassesCommand.Execute(null);
    }

    /// <summary>
    /// Determines if the reload classes command can execute
    /// </summary>
    private bool ReloadClassesCanExecute() => SelectedNamespace != null && SelectedNamespace.LoadClassesCommand.CanExecute(null);

    /// <summary>
    /// Updates the auto-generated WQL query text based on the selected class or instance
    /// </summary>
    private void UpdateAutoQueryText(object selectedObject)
    {
        var selectedClassName = SelectedClass?.ClassName ?? string.Empty;

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
        else if (selectedObject is WmiClassViewModel selectedClass)
        {
            // Create query based on just the class
            AutoQueryText = $"SELECT * FROM {selectedClassName}";
        }
        else
        {
            AutoQueryText = string.Empty;
        }
    }
}