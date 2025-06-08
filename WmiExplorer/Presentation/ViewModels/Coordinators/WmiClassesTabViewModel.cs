using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Classes tab. Manages the collection of classes
/// and related UI operations for the classes list view.
/// </summary>
public class WmiClassesTabViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private string _autoQueryText = string.Empty;
    private readonly ICacheService _cacheService;
    private readonly CancellationTokenSource _cts = new();
    private ICommand? _executeAutoQueryCommand;
    private readonly WmiInstancesTabViewModel _instancesTabViewModel;
    private WmiClassViewModel? _selectedClass;
    private WmiNamespaceViewModel? _selectedNamespace;
    private readonly ISettingsService _settingsService;
    private bool _showSystemClasses;
    private MainWindowPosition _windowPosition;
    private readonly IWmiService _wmiService;

    public WmiClassesTabViewModel(
        IMessagingService messagingService,
        ISettingsService settingsService,
        IWmiService wmiService,
        IApplicationService applicationService,
        ICacheService cacheService,
        WmiInstancesTabViewModel instancesTabViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _instancesTabViewModel = instancesTabViewModel ?? throw new ArgumentNullException(nameof(instancesTabViewModel));

        // Initialize messaging
        InitializeMessaging(messagingService);

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

        // Initialize command
        ReloadClassesCommand = new RelayCommand(
            _ => SelectedNamespace?.LoadClassesCommand.Execute(null),
            _ => SelectedNamespace != null && SelectedNamespace.LoadClassesCommand.CanExecute(null)
        );
    }

    /// <summary>
    /// Gets or sets the auto-generated WQL query text for the selected class or instance
    /// </summary>
    public string AutoQueryText
    {
        get => _autoQueryText;
        private set => SetProperty(ref _autoQueryText, value);
    }

    /// <summary>
    /// Command to execute the current query
    /// </summary>
    public ICommand ExecuteAutoQueryCommand
    {
        get
        {
            _executeAutoQueryCommand ??= new RelayCommand(
                _ => ExecuteAutoQuery(),
                _ => !string.IsNullOrWhiteSpace(AutoQueryText)
            );
            return _executeAutoQueryCommand;
        }
    }

    /// <summary>
    /// Gets the WmiInstancesTabViewModel
    /// </summary>
    public WmiInstancesTabViewModel InstancesTabViewModel => _instancesTabViewModel;

    /// <summary>
    /// Command to reload the classes of the selected namespace
    /// </summary>
    public ICommand ReloadClassesCommand { get; }

    /// <summary>
    /// Currently selected class
    /// </summary>
    public WmiClassViewModel? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value) && value != null)
            {
                // Publish message about the selected class change
                PublishMessage(new SelectedClassChangedMessage(value));

                // Update the auto-generated query
                UpdateAutoQueryText(value);
            }
            else if (value == null)
            {
                // Clear the query text when no class is selected
                AutoQueryText = string.Empty;
            }
        }
    }

    /// <summary>
    /// Currently selected namespace
    /// </summary>
    public WmiNamespaceViewModel? SelectedNamespace
    {
        get => _selectedNamespace;
        private set => SetProperty(ref _selectedNamespace, value);
    }

    /// <summary>
    /// Flag indicating whether system classes should be shown
    /// </summary>
    public bool ShowSystemClasses
    {
        get => _showSystemClasses;
        set
        {
            if (SetProperty(ref _showSystemClasses, value))
            {
                _settingsService.ShowSystemClasses = value;
            }
        }
    }

    /// <summary>
    /// Gets the window position settings
    /// </summary>
    public MainWindowPosition WindowPosition
    {
        get => _windowPosition;
        set => SetProperty(ref _windowPosition, value);
    }

    /// <summary>
    /// Executes the current WQL query
    /// </summary>
    private void ExecuteAutoQuery()
    {
        if (string.IsNullOrWhiteSpace(AutoQueryText))
            return;

        // Log the query execution
        PublishWarningState($"[Not implemented] Executing query: {AutoQueryText}");
    }

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
        if (_selectedClass != message.ClassViewModel)
        {
            _selectedClass = message.ClassViewModel;
            OnPropertyChanged(nameof(SelectedClass));

            // Update the auto-generated query when class changes
            if (_selectedClass != null)
            {
                UpdateAutoQueryText(_selectedClass);
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