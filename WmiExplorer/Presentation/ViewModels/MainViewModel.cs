using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels
{
    public class MainViewModel : MessagingViewModelBase
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ISettingsService _settingsService;
        private readonly ThemeManager _themeManager;
        private readonly IWmiService _wmiService;
        private readonly IApplicationService _applicationService;
        private readonly IWmiEventWatcherService _eventWatcherService;

        private string _temporaryComputerName = Environment.MachineName; // Temporary field for initial connection
        private ApplicationState _currentApplicationState = ApplicationState.Ready();
        private WmiNamespaceViewModel? _selectedNamespace;
        private WmiClassViewModel? _selectedClass;
        private WmiInstanceViewModel? _selectedInstance;
        private object? _selectedObject; // The selected object to show in the property grid
        private MainWindowPosition _windowPosition;
        private WmiOperationMode _operationMode = WmiOperationMode.Asynchronous;
        private WmiEventWatcherViewModel? _eventWatcherViewModel;

        public MainViewModel(
            IMessagingService messagingService,
            ISettingsService settingsService,
            ThemeManager themeManager,
            IWmiService wmiService,
            IApplicationService applicationService,
            IWmiEventWatcherService eventWatcherService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _eventWatcherService = eventWatcherService ?? throw new ArgumentNullException(nameof(eventWatcherService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            // Initialize commands
            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            ExitCommand = new RelayCommand(_ => Environment.Exit(0));
            ToggleThemeCommand = new RelayCommand(_ => _themeManager.ToggleTheme());

            // Subscribe to messages
            StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
            StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
            StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
            StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);
            StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChangedMessage);
            StrongSubscribe<SelectedEventChangedMessage>(HandleSelectedEventChangedMessage);
            StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);

            // Subscribe to theme change messages
            StrongSubscribe<ThemeChangedMessage>(_ =>
            {
                OnPropertyChanged(nameof(CurrentTheme)); // To update color-picker color on theme change.
                OnPropertyChanged(nameof(ThemeToggleText)); // To update theme toggle text on theme change.
            });

            // Initialize window position from settings
            _windowPosition = _settingsService.MainWindowPosition;

            // Initialize the Event Watcher ViewModel
            _eventWatcherViewModel = new WmiEventWatcherViewModel(messagingService, _eventWatcherService);

            // Log initial class filter
            System.Diagnostics.Debug.WriteLine($"Initialized ClassTypeFilter from settings: {_settingsService.ClassTypeFilter}");
        }

        /// <summary>
        /// Filter for WMI class types - now wraps the settings service
        /// </summary>
        public WmiClassTypeFlags ClassTypeFilter
        {
            get => _settingsService.ClassTypeFilter;
            set
            {
                // Get current value for comparison
                var currentValue = _settingsService.ClassTypeFilter;
                var newValue = value;

                // Check if the incoming value is actually a negative flag value from our converter
                // Negative values indicate a flag needs to be cleared
                if ((int)value < 0)
                {
                    // This is a signal from our converter that we need to clear a flag
                    // Convert the negative value back to a positive flag by taking its complement again
                    var flagToClear = (WmiClassTypeFlags)(~(int)value);

                    // Clear the specific flag while preserving all other flags
                    newValue = currentValue & ~flagToClear;
                }
                else if ((int)value > 0 && (int)value <= (int)WmiClassTypeFlags.All)
                {
                    // This is a positive flag value coming from the converter when a checkbox is checked
                    // Set this flag while preserving all other flags
                    newValue = currentValue | value;
                }

                // Only update if the value actually changed
                if (currentValue != newValue)
                {
                    // Update the setting - this will handle save and notifications
                    _settingsService.ClassTypeFilter = newValue;

                    // Notify UI of property change
                    OnPropertyChanged(nameof(ClassTypeFilter));

                    System.Diagnostics.Debug.WriteLine($"ClassTypeFilter updated to: {newValue}");
                }
            }
        }

        /// <summary>
        /// Target computer name for WMI connection - used for the text box input only
        /// </summary>
        public string ComputerName
        {
            get => _temporaryComputerName;
            set => SetProperty(ref _temporaryComputerName, value);
        }

        /// <summary>
        /// Command to connect to a WMI namespace
        /// </summary>
        public ICommand ConnectCommand { get; }

        /// <summary>
        /// The current application state
        /// </summary>
        public ApplicationState CurrentApplicationState
        {
            get => _currentApplicationState;
            set => SetProperty(ref _currentApplicationState, value);
        }

        /// <summary>
        /// Gets the current theme object
        /// </summary>
        public Theme CurrentTheme => _themeManager.CurrentThemeObject!;

        /// <summary>
        /// Command to exit the application
        /// </summary>
        public ICommand ExitCommand { get; }

        /// <summary>
        /// Collection of WMI namespaces in the tree
        /// </summary>
        public ObservableCollection<WmiNamespaceViewModel> Namespaces { get; } = new();

        /// <summary>
        /// Currently selected namespace in the tree
        /// </summary>
        public WmiNamespaceViewModel? SelectedNamespace
        {
            get => _selectedNamespace;
            set
            {
                if (SetProperty(ref _selectedNamespace, value) && value != null)
                {
                    // Make sure selected namespaces are expanded
                    if (!value.IsExpanded)
                    {
                        value.IsExpanded = true;
                    }

                    // Selected namespace is now handled directly by the ViewModel's IsSelected property
                }
            }
        }

        /// <summary>
        /// Currently selected class in the tree
        /// </summary>
        public WmiClassViewModel? SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }

        /// <summary>
        /// Currently selected instance in the property grid
        /// </summary>
        public WmiInstanceViewModel? SelectedInstance
        {
            get => _selectedInstance;
            set => SetProperty(ref _selectedInstance, value);
        }

        /// <summary>
        /// Object to display in the property grid - could be namespace, class, or instance
        /// </summary>
        public object? SelectedObject
        {
            get => _selectedObject;
            set
            {
                if (SetProperty(ref _selectedObject, value))
                {
                    // Notify that the display name has changed when the selected object changes
                    OnPropertyChanged(nameof(SelectedObjectDisplayName));
                }
            }
        }

        /// <summary>
        /// Gets the display name of the currently selected object for the property grid header
        /// </summary>
        public string SelectedObjectDisplayName
        {
            get
            {
                if (_selectedObject == null)
                    return "No Selection";

                if (_selectedObject is WmiNamespaceViewModel namespaceVm)
                    return $"Namespace: {namespaceVm.Name}";

                if (_selectedObject is WmiClassViewModel classVm)
                    return $"Class: {classVm.ClassName}";

                if (_selectedObject is WmiInstanceViewModel instanceVm)
                    return $"Instance: {instanceVm.InstanceName}";

                return _selectedObject.GetType().Name;
            }
        }

        /// <summary>
        /// Gets the text for the theme toggle button
        /// </summary>
        public string ThemeToggleText => _themeManager.CurrentThemeName == "Dark" ? "🌙 Dark" : "🌞 Light";

        /// <summary>
        /// Command to toggle between light and dark theme
        /// </summary>
        public ICommand ToggleThemeCommand { get; }

        /// <summary>
        /// Gets the window position settings
        /// </summary>
        public MainWindowPosition WindowPosition
        {
            get => _windowPosition;
            set => SetProperty(ref _windowPosition, value);
        }

        /// <summary>
        /// Gets or sets the operation mode for WMI operations
        /// </summary>
        public WmiOperationMode OperationMode
        {
            get => _operationMode;
            set
            {
                if (SetProperty(ref _operationMode, value))
                {
                    _wmiService.OperationMode = value; // Propagate to service
                }
            }
        }

        /// <summary>
        /// Gets the view model for the WMI Event Watcher
        /// </summary>
        public WmiEventWatcherViewModel EventWatcherViewModel => _eventWatcherViewModel!;

        /// <summary>
        /// Connects to the specified computer or namespace path
        /// </summary>
        private async Task ConnectAsync()
        {
            string input = _temporaryComputerName.Trim();

            // Parse the input to determine what type of connection we're making
            string effectivePath;

            try
            {
                // Normalize the input and determine the path type
                if (string.IsNullOrEmpty(input) || input == "." || input.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    // Case 1: Local machine - display as \\COMPUTERNAME\ROOT
                    effectivePath = @"\\.\ROOT";
                }
                else if (input.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                {
                    // Case 4: Full WMI path with computer - use as is
                    // Format: \\computer\namespace (e.g., \\computer\root\cimv2)
                    effectivePath = input;
                }
                else if (input.Contains("\\"))
                {
                    // Case 3: Namespace path without computer - assume local computer
                    // Format: root\namespace (e.g., root\cimv2)
                    effectivePath = $@"\\.\{input}";
                }
                else
                {
                    // Case 2: Computer name only - display as \\COMPUTERNAME\ROOT
                    effectivePath = $@"\\{input}\ROOT";
                }

                PublishBusyState($"Connecting to {effectivePath}...");

                // Check if we're already connected to this path
                var existingRoot = Namespaces.FirstOrDefault(n =>
                    n.NamespacePath.Equals(effectivePath, StringComparison.OrdinalIgnoreCase));

                if (existingRoot != null)
                {
                    // Just select the existing root namespace
                    SelectedNamespace = existingRoot;
                    PublishSuccessState($"Connected to {effectivePath}");
                    return;
                }

                // Force garbage collection to release any existing WMI resources
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Create the root namespace view model using the async method
                var rootViewModel = await WmiNamespaceViewModel.CreateRootAsync(
                    effectivePath,
                    _wmiService,
                    MessageService!,
                    _applicationService,
                    _settingsService,
                    _cts.Token);

                // Load initial children
                await rootViewModel.ExpandAsync();

                // Add to the UI collection
                await RunOnUIThreadAsync(() =>
                {
                    Namespaces.Add(rootViewModel);
                    SelectedNamespace = rootViewModel; // Select the root namespace
                    return Task.CompletedTask;
                });

                PublishSuccessState($"Connected to {effectivePath}");
            }
            catch (Exception ex)
            {
                PublishErrorState($"Error connecting to {input}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles application state messages
        /// </summary>
        private void HandleApplicationStateMessage(ApplicationStateMessage message)
        {
            // Ensure application state updates happen on the UI thread
            RunOnUIThread(() =>
            {
                CurrentApplicationState = message.State;
            });

            // Log state change for debugging
            System.Diagnostics.Debug.WriteLine($"Application state changed: {message.State.State}, Message: {message.State.Message}");
        }

        /// <summary>
        /// Updates the status bar message based on the selected namespace's load states
        /// </summary>
        private void UpdateNamespaceStatus(WmiNamespaceViewModel? ns)
        {
            if (ns == null)
                return;

            if (ns.ClassLoadState == ClassLoadState.Unknown)
            {
                PublishErrorState($"Selected {ns.NamespacePath} Double-click to load classes.");
                return;
            }
            if (ns.ClassLoadState == ClassLoadState.Loading)
            {
                PublishBusyState($"Loading classes for {ns.NamespacePath}...");
                return;
            }
            if (ns.ClassLoadState == ClassLoadState.Failed)
            {
                PublishErrorState($"Failed to load classes for {ns.NamespacePath}. Double-click namespace to try again.");
                return;
            }
            if (ns.ClassLoadState == ClassLoadState.Success && ns.NamespaceLoadState == NamespaceLoadState.Success)
            {
                var count = ns.ClassesView.Cast<object>().Count();
                PublishSuccessState($"Showing {count} classes for {ns.NamespacePath}");
                return;
            }
        }

        /// <summary>
        /// Handles when a namespace is selected to ensure it loads its children and updates the class count/status message
        /// </summary>
        private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
        {
            if (message?.NamespaceViewModel == null)
                return;

            // Make sure we always trigger expansion, even for already selected items
            if (!message.NamespaceViewModel.IsExpanded)
            {
                message.NamespaceViewModel.IsExpanded = true;
            }

            // Make sure our local SelectedNamespace property is synchronized
            if (_selectedNamespace != message.NamespaceViewModel)
            {
                SelectedNamespace = message.NamespaceViewModel;
            }

            // Update the selected object for the property grid
            SelectedObject = message.NamespaceViewModel.WmiNamespace;

            // Use the new method to update the status bar
            UpdateNamespaceStatus(message.NamespaceViewModel);
        }

        /// <summary>
        /// Handles when a class is selected to update the property grid
        /// </summary>
        private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
        {
            if (message?.ClassViewModel == null)
                return;

            // Update our selected class property
            SelectedClass = message.ClassViewModel;

            // Update the selected object for the property grid
            SelectedObject = message.ClassViewModel.WmiClass;
        }

        /// <summary>
        /// Handles when an instance is selected to update the property grid
        /// </summary>
        private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
        {
            if (message?.InstanceViewModel == null)
                return;

            // Update our selected instance property which is bound to the property grid
            SelectedInstance = message.InstanceViewModel;

            // Update the selected object for the property grid
            SelectedInstance.WmiInstance.ActualObject?.Get();
            SelectedObject = message.InstanceViewModel.WmiInstance;
        }

        /// <summary>
        /// Handles class type filter changes
        /// </summary>
        private void HandleClassTypeFilterChangedMessage(ClassTypeFilterChangedMessage message)
        {
            if (message == null) return;

            // Update UI if needed
            OnPropertyChanged(nameof(ClassTypeFilter));

            System.Diagnostics.Debug.WriteLine($"MainViewModel received ClassTypeFilterChanged: {_settingsService.ClassTypeFilter}");
        }

        /// <summary>
        /// Handles when a WMI event is selected to update the property grid
        /// </summary>
        private void HandleSelectedEventChangedMessage(SelectedEventChangedMessage message)
        {
            if (message?.WmiEvent == null)
                return;

            // Update the selected object for the property grid
            SelectedObject = message.WmiEvent;
        }

        /// <summary>
        /// Handles when classes are filtered in the selected namespace to update the status bar
        /// </summary>
        private void HandleClassesFilteredMessage(ClassesFilteredMessage message)
        {
            if (message?.NamespaceViewModel != null && message.NamespaceViewModel == SelectedNamespace)
            {
                UpdateNamespaceStatus(message.NamespaceViewModel);
            }
        }

        /// <summary>
        /// Override to clean up additional resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel any pending operations
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                // Dispose the cancellation token source
                _cts.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Command to reload the classes of the selected namespace
        /// </summary>
        public ICommand ReloadClassesCommand => new RelayCommand(
            _ => SelectedNamespace?.LoadClassesCommand.Execute(null),
            _ => SelectedNamespace != null && SelectedNamespace.LoadClassesCommand.CanExecute(null)
        );
    }
}