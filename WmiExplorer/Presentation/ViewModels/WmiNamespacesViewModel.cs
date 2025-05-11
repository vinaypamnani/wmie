using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    // Enum moved from WmiNamespaceWrapper
    public enum NamespaceLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    /// <summary>
    /// Status of the class loading process
    /// </summary>
    public enum ClassLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    /// <summary>
    /// Combined load state for UI binding that represents the overall state
    /// </summary>
    public enum LoadState
    {
        Unknown,
        Loading,
        Success,
        PartialSuccess,
        Failed
    }

    /// <summary>
    /// ViewModel for WMI namespaces that handles both UI presentation and data operations
    /// </summary>
    public class WmiNamespacesViewModel : MessagingViewModelBase
    {
        // Core model and services
        private readonly WmiNamespace _model;
        private readonly IWmiService _wmiService;
        private readonly IApplicationService _applicationService;
        private readonly ISettingsService _settingsService;
        private string _computerName = string.Empty;
        private string _displayName = string.Empty;

        // Parent reference
        private WmiNamespacesViewModel? _parentNamespace;

        // State fields 
        private bool _hasLoadedChildren;
        private bool _isExpanded;
        private bool _isSelected;
        private ObservableCollection<WmiClassesViewModel> _classes = new();
        private ICollectionView? _classesView;
        private WmiClassesViewModel? _selectedClass;
        private string _quickFilterClasses = string.Empty;
        private string _pendingQuickFilter = string.Empty;
        private DispatcherTimer? _filterTimer;
        private NamespaceLoadState _namespaceLoadState = NamespaceLoadState.Unknown;
        private ClassLoadState _classLoadState = ClassLoadState.Unknown;
        private CancellationTokenSource _cts = new();        

        // ManagementScope for WMI operations (created on demand)
        private ManagementScope? _managementScope;
        public ManagementScope ManagementScope
        {
            get
            {
                if (_managementScope == null)
                {
                    var options = _model.ConnectionOptions;
                    var scopePath = _model.NamespacePath;
                    _managementScope = _wmiService.CreateManagementScope(scopePath, options);
                }
                return _managementScope;
            }
        }

        // Commands
        private ICommand? _loadClassesCommand;
        private ICommand? _expandCommand;
        private ICommand? _copyRelativePathCommand;

        // Children collection for the hierarchical tree view
        public ObservableCollection<WmiNamespacesViewModel> Children { get; } = new();

        #region Constructors

        /// <summary>
        /// Regular constructor for namespace instances
        /// </summary>
        public WmiNamespacesViewModel(
            WmiNamespace model,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            ISettingsService settingsService)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            // Initialize the collection view for classes
            _classesView = CollectionViewSource.GetDefaultView(_classes);

            // Set up filtering
            if (_classesView != null)
            {
                _classesView.Filter = QuickFilterClassesPredicate;
            }

            // Initialize filter timer for debouncing
            _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _filterTimer.Tick += OnFilterTimerTick;

            // Subscribe to messages using StrongSubscribe
            StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
            StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChanged, true); // Run on UI thread
        }

        /// <summary>
        /// Factory method to create a collection of WmiNamespacesViewModels from a collection of ManagementObject
        /// </summary>
        public static ObservableCollection<WmiNamespacesViewModel> CreateFromCollection(
            IEnumerable<ManagementObject> mboCollection,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            ISettingsService settingsService,
            WmiNamespace parent)
        {
            if (mboCollection == null)
                throw new ArgumentNullException(nameof(mboCollection));

            var viewModels = new ObservableCollection<WmiNamespacesViewModel>();

            foreach (var mo in mboCollection)
            {
                string nsPath = mo.Properties["Name"]?.Value is string name && mo.Scope != null && mo.Scope.Path != null
                    ? $"{mo.Scope.Path.Path}\\{name}"
                    : string.Empty;
                var model = new WmiNamespace(mo, nsPath, parent);
                viewModels.Add(new WmiNamespacesViewModel(
                    model,
                    wmiService,
                    messagingService,
                    applicationService,
                    settingsService));
            }

            return viewModels;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the full path of the namespace
        /// </summary>
        public string FullPath => _model?.NamespacePath ?? string.Empty;

        /// <summary>
        /// Gets the ManagementObject of the namespace if available
        /// </summary>
        public ManagementObject? ActualObject => _model?.ActualObject;

        /// <summary>
        /// Gets the name of the namespace
        /// </summary>
        public string Name
        {
            get
            {
                // For root namespace, use the display name if it's set
                if (!string.IsNullOrEmpty(_displayName) && _model?.IsRoot == true)
                {
                    return _displayName;
                }

                if (ActualObject != null && ActualObject.Properties["Name"]?.Value is string name)
                {
                    return name;
                }

                return FullPath;
            }
        }        

        /// <summary>
        /// Gets or sets the computer name for this namespace
        /// </summary>
        public string ComputerName
        {
            get => _computerName;
            set => SetProperty(ref _computerName, value);
        }

        /// <summary>
        /// Gets or sets the display name for this namespace
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// Gets or sets whether this namespace has loaded its children
        /// </summary>
        public bool HasLoadedChildren
        {
            get => _hasLoadedChildren;
            set => SetProperty(ref _hasLoadedChildren, value);
        }

        /// <summary>
        /// Gets or sets whether this namespace is expanded in the tree view
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    // Automatically call expand when IsExpanded is set to true
                    ExpandCommand?.Execute(null);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this namespace is selected in the tree view
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && value)
                {
                    // Notify when this namespace is selected
                    NotifyNamespaceSelected();
                }
            }
        }

        /// <summary>
        /// Gets or sets the load status for this namespace
        /// </summary>
        public NamespaceLoadState NamespaceLoadState
        {
            get => _namespaceLoadState;
            set
            {
                if (SetProperty(ref _namespaceLoadState, value))
                {
                    // Notify that LoadState has changed
                    OnPropertyChanged(nameof(LoadState));
                }
            }
        }

        /// <summary>
        /// Gets or sets the load status for classes in this namespace
        /// </summary>
        public ClassLoadState ClassLoadState
        {
            get => _classLoadState;
            set
            {
                if (SetProperty(ref _classLoadState, value))
                {
                    // Notify that LoadState has changed
                    OnPropertyChanged(nameof(LoadState));
                }
            }
        }

        /// <summary>
        /// Collection of classes in this namespace
        /// </summary>
        public ObservableCollection<WmiClassesViewModel> Classes
        {
            get => _classes;
            set => SetProperty(ref _classes, value);
        }

        /// <summary>
        /// The filtered view of classes
        /// </summary>
        public ICollectionView ClassesView => _classesView ?? (_classesView = CollectionViewSource.GetDefaultView(Classes));

        /// <summary>
        /// Currently selected class in this namespace
        /// </summary>
        public WmiClassesViewModel? SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetProperty(ref _selectedClass, value) && value != null)
                {
                    // Notify about selection change
                    PublishMessage(new SelectedClassChangedMessage(value));
                }
            }
        }

        /// <summary>
        /// Quick filter text for filtering classes
        /// </summary>
        public string QuickFilterClasses
        {
            get => _pendingQuickFilter;
            set
            {
                if (SetProperty(ref _pendingQuickFilter, value))
                {
                    // Use debounce timer for smoother filtering experience
                    _filterTimer?.Stop();
                    _filterTimer?.Start();
                }
            }
        }

        /// <summary>
        /// Gets or sets the parent namespace
        /// </summary>
        public WmiNamespacesViewModel? ParentNamespace
        {
            get => _parentNamespace;
            set => SetProperty(ref _parentNamespace, value);
        }

        /// <summary>
        /// Combined load state for UI binding that incorporates both namespace and class load states
        /// </summary>
        public LoadState LoadState
        {
            get
            {
                // If either state is loading, the combined state is loading
                if (_namespaceLoadState == NamespaceLoadState.Loading || _classLoadState == ClassLoadState.Loading)
                    return LoadState.Loading;

                // If both states are successful, combined state is success
                if (_namespaceLoadState == NamespaceLoadState.Success && _classLoadState == ClassLoadState.Success)
                    return LoadState.Success;

                // If one state is successful but the other isn't, it's partial success
                // (as long as neither is in the Failed state)
                if ((_namespaceLoadState == NamespaceLoadState.Success || _classLoadState == ClassLoadState.Success)
                    && _namespaceLoadState != NamespaceLoadState.Failed
                    && _classLoadState != ClassLoadState.Failed)
                    return LoadState.PartialSuccess;

                // If either state is failed, the combined state is failed
                if (_namespaceLoadState == NamespaceLoadState.Failed || _classLoadState == ClassLoadState.Failed)
                    return LoadState.Failed;

                // Default is unknown if both states are unknown
                return LoadState.Unknown;
            }
        }

        #endregion

        #region Commands

        // Commands for UI interaction
        public ICommand LoadClassesCommand
        {
            get => _loadClassesCommand ?? (_loadClassesCommand = new AsyncRelayCommand(LoadClassesAsync));
            set => _loadClassesCommand = value;
        }

        public ICommand ExpandCommand
        {
            get => _expandCommand ?? (_expandCommand = new AsyncRelayCommand(ExpandAsync));
            set => _expandCommand = value;
        }

        public ICommand CopyRelativePathCommand
        {
            get => _copyRelativePathCommand ?? (_copyRelativePathCommand = new RelayCommand(_ => CopyRelativePath()));
            set => _copyRelativePathCommand = value;
        }

        #endregion

        #region Root Creation

        /// <summary>
        /// Create a root namespace view model for the main hierarchy
        /// </summary>
        public static async Task<WmiNamespacesViewModel> CreateRootAsync(
            string namespacePath,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            ISettingsService settingsService,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(namespacePath))
                throw new ArgumentException("Namespace path cannot be empty", nameof(namespacePath));

            // Use the WmiService to get the actual namespace object with real management object
            var rootMbo = await wmiService.GetRootNamespaceAsync(namespacePath, cancellationToken);
            var rootModel = new WmiNamespace(rootMbo, namespacePath, new ConnectionOptions());
            // Create the view model with all services
            var rootViewModel = new WmiNamespacesViewModel(
                rootModel,
                wmiService,
                messagingService,
                applicationService,
                settingsService);

            // Optionally set ComputerName and DisplayName from the namespacePath
            if (rootMbo?.Scope?.Path != null)
                rootViewModel.ComputerName = rootMbo.Scope.Path.Server;
            rootViewModel.DisplayName = namespacePath;

            return rootViewModel;
        }

        private static string ExtractComputerNameFromPath(string namespacePath)
        {
            // Expecting format: \\COMPUTER\root... or \\.\root...
            if (namespacePath.StartsWith("\\"))
            {
                var parts = namespacePath.Split('\\');
                if (parts.Length > 2)
                    return parts[2];
            }
            return string.Empty;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Notify that this namespace is selected
        /// </summary>
        private void NotifyNamespaceSelected()
        {
            // Publish the message about the selection
            PublishMessage(new SelectedNamespaceChangedMessage(this));
        }

        /// <summary>
        /// Force selection notification even when already selected
        /// </summary>
        public void ForceSelection()
        {
            // Always publish the message even if already selected
            NotifyNamespaceSelected();
        }

        /// <summary>
        /// Handle class selection message and update selected class if needed
        /// </summary>
        private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
        {
            if (message?.ClassViewModel == null) return;

            // If this namespace contains the selected class, select it
            if (Classes.Contains(message.ClassViewModel) &&
                SelectedClass != message.ClassViewModel)
            {
                SelectedClass = message.ClassViewModel;
            }
        }

        /// <summary>
        /// Handle class filter changes from settings and reload as needed
        /// </summary>
        private void HandleClassTypeFilterChanged(ClassTypeFilterChangedMessage message)
        {
            if (message == null) return;

            // If this namespace is selected and classes were previously loaded (regardless of count),
            // reload them with the new filter
            if (_classLoadState != ClassLoadState.Unknown && _isSelected)
            {
                // Add debug logging to track message receipt
                System.Diagnostics.Debug.WriteLine($"WmiNamespacesViewModel ({FullPath}) received ClassTypeFilterChanged: {message.ClassTypeFilter}");

                // Clear existing classes
                Classes.Clear();

                // Reload classes with the new filter
                _ = LoadClassesAsync();
            }
        }

        /// <summary>
        /// Copy the namespace path to clipboard
        /// </summary>
        private void CopyRelativePath()
        {
            if (string.IsNullOrEmpty(FullPath))
                return;

            _applicationService.CopyToClipboard(FullPath);
            PublishSuccessState($"Copied path: {FullPath}");
        }

        /// <summary>
        /// Filter timer tick handler
        /// </summary>
        private void OnFilterTimerTick(object? sender, EventArgs e)
        {
            _filterTimer?.Stop();

            if (_quickFilterClasses != _pendingQuickFilter)
            {
                _quickFilterClasses = _pendingQuickFilter;
                _classesView?.Refresh();
            }
        }

        /// <summary>
        /// Predicate for filtering classes based on quick filter text
        /// </summary>
        private bool QuickFilterClassesPredicate(object item)
        {
            if (string.IsNullOrWhiteSpace(_quickFilterClasses))
                return true;

            if (item is WmiClassesViewModel classVm)
            {
                // Fast path for single character
                if (_quickFilterClasses.Length == 1)
                {
                    return classVm.ClassName.IndexOf(_quickFilterClasses, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                // Use IndexOf for better performance with longer strings
                return classVm.ClassName.IndexOf(_quickFilterClasses, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        /// <summary>
        /// Expand this namespace and load child namespaces
        /// </summary>
        public async Task ExpandAsync()
        {
            if (HasLoadedChildren)
            {
                IsExpanded = true;
                return;
            }

            try
            {
                NamespaceLoadState = NamespaceLoadState.Loading;
                PublishBusyState($"Loading {FullPath}...");

                // Use the ViewModel's ManagementScope for the service call
                var childNamespaces = await _wmiService.GetChildNamespacesAsync(
                    ManagementScope,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Create view models for each child namespace using the factory method
                var childViewModels = CreateFromCollection(
                    childNamespaces,
                    _wmiService,
                    MessageService!,
                    _applicationService,
                    _settingsService,
                    _model);

                await RunOnUIThreadAsync(() =>
                {
                    Children.Clear();
                    foreach (var childViewModel in childViewModels)
                    {
                        // Set this namespace as the parent of each child namespace
                        childViewModel.ParentNamespace = this;
                        Children.Add(childViewModel);
                    }
                    return Task.CompletedTask;
                });

                HasLoadedChildren = true;
                IsExpanded = true;
                NamespaceLoadState = NamespaceLoadState.Success;
                PublishSuccessState($"Loaded children for {FullPath}");
            }
            catch (OperationCanceledException)
            {
                NamespaceLoadState = NamespaceLoadState.Failed;
                PublishErrorState($"Loading {FullPath} was canceled");
            }
            catch (Exception ex)
            {
                NamespaceLoadState = NamespaceLoadState.Failed;
                PublishErrorState($"Error loading {FullPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load classes for this namespace
        /// </summary>
        public async Task LoadClassesAsync()
        {
            // We remove the check for Classes.Count to enable reloading even when previous result had 0 classes
            try
            {
                ClassLoadState = ClassLoadState.Loading;
                PublishBusyState($"Loading classes for {FullPath}...");

                // Get the class type filter from settings
                var classTypeFilter = _settingsService.ClassTypeFilter;

                // Use the ViewModel's ManagementScope for the service call
                var wmiClasses = await _wmiService.GetClassesAsync(
                    ManagementScope,
                    classTypeFilter,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Use the factory method to create view models for all classes at once
                var classModels = wmiClasses.Select(mo => new WmiClass(mo));
                var classViewModels = WmiClassesViewModel.CreateFromCollection(
                    classModels,
                    _wmiService,
                    MessageService!,
                    _applicationService);

                // Sort the collection by class name
                var sortedClassViewModels = new ObservableCollection<WmiClassesViewModel>(
                    classViewModels.OrderBy(vm => vm.ClassName)
                );

                // Update the UI collection
                await RunOnUIThreadAsync(() =>
                {
                    Classes.Clear();
                    foreach (var classVm in sortedClassViewModels)
                    {
                        // Set this namespace as the parent of each class
                        classVm.ParentNamespace = this;
                        Classes.Add(classVm);
                    }

                    // Refresh the collection view
                    ClassesView.Refresh();
                    return Task.CompletedTask;
                });

                ClassLoadState = ClassLoadState.Success;
                PublishSuccessState($"Loaded {sortedClassViewModels.Count} classes for {FullPath}");
            }
            catch (OperationCanceledException)
            {
                ClassLoadState = ClassLoadState.Failed;
                PublishErrorState($"Loading classes for {FullPath} was canceled");
            }
            catch (Exception ex)
            {
                ClassLoadState = ClassLoadState.Failed;
                PublishErrorState($"Error loading {FullPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Returns the namespace's string representation
        /// </summary>
        public override string ToString() => _model?.ToString() ?? string.Empty;

        /// <summary>
        /// Override to clean up additional resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();

                // Clean up timer
                if (_filterTimer != null)
                {
                    _filterTimer.Stop();
                    _filterTimer.Tick -= OnFilterTimerTick;
                    _filterTimer = null;
                }
                // No need to dispose _managementScope; ManagementScope does not implement IDisposable
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}