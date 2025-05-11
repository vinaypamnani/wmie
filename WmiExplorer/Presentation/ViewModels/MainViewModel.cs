using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
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
        
        private string _temporaryComputerName = Environment.MachineName; // Temporary field for initial connection
        private ApplicationState _currentApplicationState = ApplicationState.Ready();
        private WmiNamespacesViewModel? _selectedNamespace;
        private WmiClassesViewModel? _selectedClass;
        private WmiInstancesViewModel? _selectedInstance;
        private object? _selectedObject; // The selected object to show in the property grid
        private MainWindowPosition _windowPosition;

        public MainViewModel(
            IMessagingService messagingService,
            ISettingsService settingsService,
            ThemeManager themeManager,
            IWmiService wmiService,
            IApplicationService applicationService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));

            // Initialize messaging
            InitializeMessaging(messagingService);
            
            // Initialize commands
            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            ExitCommand = new RelayCommand(_ => Environment.Exit(0));
            ToggleThemeCommand = new RelayCommand(_ => _themeManager.ToggleTheme());

            // Subscribe to messages - MainViewModel only cares about application state and namespace changes
            StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
            StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
            StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
            StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);
            StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChangedMessage);

            // Initialize window position from settings
            _windowPosition = _settingsService.MainWindowPosition;

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
        /// Gets the current theme name
        /// </summary>
        public string CurrentTheme => _themeManager.CurrentTheme;

        /// <summary>
        /// Command to exit the application
        /// </summary>
        public ICommand ExitCommand { get; }

        /// <summary>
        /// Collection of WMI namespaces in the tree
        /// </summary>
        public ObservableCollection<WmiNamespacesViewModel> Namespaces { get; } = new();

        /// <summary>
        /// Currently selected namespace in the tree
        /// </summary>
        public WmiNamespacesViewModel? SelectedNamespace
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
        public WmiClassesViewModel? SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }

        /// <summary>
        /// Currently selected instance in the property grid
        /// </summary>
        public WmiInstancesViewModel? SelectedInstance
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
                
                if (_selectedObject is WmiNamespacesViewModel namespaceVm)
                    return $"Namespace: {namespaceVm.Name}";
                
                if (_selectedObject is WmiClassesViewModel classVm)
                    return $"Class: {classVm.ClassName}";
                
                if (_selectedObject is WmiInstancesViewModel instanceVm)
                    return $"Instance: {instanceVm.InstanceName}";
                
                return _selectedObject.GetType().Name;
            }
        }

        /// <summary>
        /// Gets the text for the theme toggle button
        /// </summary>
        public string ThemeToggleText => _themeManager.ThemeToggleText;

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
        /// Connects to the specified computer or namespace path
        /// </summary>
        private async Task ConnectAsync()
        {
            string input = _temporaryComputerName.Trim();
            
            // Parse the input to determine what type of connection we're making
            string displayName;
            string effectivePath;
            string effectiveComputerName;
            
            try
            {
                // Normalize the input and determine the path type
                if (string.IsNullOrEmpty(input) || input == "." || input.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    // Case 1: Local machine - display as \\COMPUTERNAME\ROOT
                    effectiveComputerName = Environment.MachineName;
                    effectivePath = @"\\.\ROOT";
                    displayName = $@"\\{effectiveComputerName}\ROOT";
                }
                else if (input.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
                {
                    // Case 4: Full WMI path with computer - use as is
                    // Format: \\computer\namespace (e.g., \\computer\root\cimv2)
                    effectivePath = input;
                    displayName = input;
                    
                    // Extract computer name from the path
                    var parts = input.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    effectiveComputerName = parts.Length > 0 ? parts[0] : Environment.MachineName;
                }
                else if (input.Contains("\\"))
                {
                    // Case 3: Namespace path without computer - assume local computer
                    // Format: root\namespace (e.g., root\cimv2)
                    effectivePath = $@"\\.\{input}";
                    displayName = input;
                    effectiveComputerName = Environment.MachineName;
                }
                else
                {
                    // Case 2: Computer name only - display as \\COMPUTERNAME\ROOT
                    effectiveComputerName = input;
                    effectivePath = $@"\\{input}\ROOT";
                    displayName = effectivePath;
                }

                PublishBusyState($"Connecting to {displayName}...");

                // Check if we're already connected to this path
                var existingRoot = Namespaces.FirstOrDefault(n => 
                    n.FullPath.Equals(effectivePath, StringComparison.OrdinalIgnoreCase));
                
                if (existingRoot != null)
                {
                    // Just select the existing root namespace
                    SelectedNamespace = existingRoot;
                    PublishSuccessState($"Connected to {displayName}");
                    return;
                }

                // Force garbage collection to release any existing WMI resources
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // Create the root namespace view model using the async method
                var rootViewModel = await WmiNamespacesViewModel.CreateRootAsync(
                    effectiveComputerName,
                    _wmiService,
                    MessageService!,
                    _applicationService,
                    _settingsService,
                    _cts.Token,
                    effectivePath,
                    displayName);

                // Load initial children
                await rootViewModel.ExpandAsync();

                // Add to the UI collection
                await RunOnUIThreadAsync(() =>
                {
                    Namespaces.Add(rootViewModel);
                    SelectedNamespace = rootViewModel; // Select the root namespace
                    return Task.CompletedTask;
                });

                PublishSuccessState($"Connected to {displayName}");
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
        /// Handles when a namespace is selected to ensure it loads its children
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
            SelectedObject = message.NamespaceViewModel;
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
            SelectedObject = message.ClassViewModel;
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
            SelectedObject = message.InstanceViewModel;
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
    }
}