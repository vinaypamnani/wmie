using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Classes tab. Manages the collection of classes
/// and related UI operations for the classes list view.
/// </summary>
public partial class ClassesTabViewModel : MessagingViewModel
{
    private readonly IApplicationService _applicationService;

    [ObservableProperty]
    private string _autoQueryText = string.Empty;

    private readonly ICacheService _cacheService;
    private readonly CancellationTokenSource _cts = new();
    private readonly WmiInstancesTabViewModel _instancesTabViewModel;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadClassesCommand))]
    private WmiNamespaceViewModel? _selectedNamespace;

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
        WmiInstancesTabViewModel instancesTabViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _instancesTabViewModel = instancesTabViewModel ?? throw new ArgumentNullException(nameof(instancesTabViewModel));

        // Subscribe to messages
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
        StrongSubscribe<ClassesLoadedMessage>(HandleClassesLoadedMessage);
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);
        StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);

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
    /// Gets the WmiInstancesTabViewModel
    /// </summary>
    public WmiInstancesTabViewModel InstancesTabViewModel => _instancesTabViewModel;

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
    /// Handles the SelectedClassChangedMessage
    /// </summary>
    private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
    {
        // Only update if it's not already the selected class to avoid circular updates
        if (SelectedClass != message.ClassViewModel)
        {
            SelectedClass = message.ClassViewModel;

            // Update the auto-generated query when class changes
            if (SelectedClass != null)
            {
                UpdateAutoQueryText(SelectedClass);
            }
        }
    }

    /// <summary>
    /// Handles when an instance is selected to update the auto-generated query
    /// </summary>
    private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
    {
        if (message?.InstanceViewModel == null)
            return;

        // Update the auto-generated query
        UpdateAutoQueryText(message.InstanceViewModel);
    }

    /// <summary>
    /// Handles the SelectedNamespaceChangedMessage
    /// </summary>
    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        SelectedNamespace = message.NamespaceViewModel;

        // Reset selected class when namespace changes
        SelectedClass = null;
    }

    partial void OnAutoQueryTextChanged(string value)
    {
        // Notify that the CanExecute state of ExecuteAutoQueryCommand may have changed
        ExecuteAutoQueryCommand.NotifyCanExecuteChanged();
    }

    // Property change notification methods
    partial void OnSelectedClassChanged(WmiClassViewModel? value)
    {
        if (value != null)
        {
            // Publish message about the selected class change
            PublishMessage(new SelectedClassChangedMessage(value));

            // Update the auto-generated query
            UpdateAutoQueryText(value);
        }
        else
        {
            // Clear the query text when no class is selected
            AutoQueryText = string.Empty;
        }
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