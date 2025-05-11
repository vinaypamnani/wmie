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
    // Define an enum for class load status
    public enum InstanceLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    public class WmiClassesViewModel : MessagingViewModelBase
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IWmiService _wmiService;
        private readonly IApplicationService _applicationService;
        private readonly WmiClass _model;
        
        private DispatcherTimer? _filterTimer;
        private string? _description;
        
        // Parent reference
        private WmiNamespacesViewModel? _parentNamespace;
        
        private ObservableCollection<WmiInstancesViewModel> _instances = new();
        private ICollectionView? _wmiInstancesView;
        private string _pendingQuickFilter = string.Empty;
        private string _quickFilterInstances = string.Empty;
        private WmiInstancesViewModel? _selectedInstance;
        private InstanceLoadState _loadState = InstanceLoadState.Unknown;

        /// <summary>
        /// Constructor for a single WmiClass
        /// </summary>
        public WmiClassesViewModel(
            WmiClass model,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            LoadInstancesCommand = new AsyncRelayCommand(LoadInstancesAsync);
            CopyRelativePathCommand = new RelayCommand(CopyRelativePath);

            // Subscribe to messages using StrongSubscribe
            StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);

            // Initialize the collection view for filtering
            _wmiInstancesView = CollectionViewSource.GetDefaultView(_instances);
            _wmiInstancesView.Filter = QuickFilterInstancesPredicate;

            // Initialize filter timer for debouncing
            _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _filterTimer.Tick += OnFilterTimerTick;
        }
        
        /// <summary>
        /// Factory method to create a collection of WmiClassesViewModels from a collection of WmiClass models
        /// </summary>
        public static ObservableCollection<WmiClassesViewModel> CreateFromCollection(
            IEnumerable<WmiClass> models,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            if (models == null)
                throw new ArgumentNullException(nameof(models));
                
            var viewModels = new ObservableCollection<WmiClassesViewModel>();
            
            foreach (var model in models)
            {
                viewModels.Add(new WmiClassesViewModel(
                    model,
                    wmiService,
                    messagingService,
                    applicationService));
            }
            
            return viewModels;
        }

        #region Properties from former WmiClassWrapper

        /// <summary>
        /// Gets the class name
        /// </summary>
        public string ClassName => _model.ClassName;

        /// <summary>
        /// Gets the namespace path containing this class
        /// </summary>
        public string ClassPath => _model.ClassPath;

        /// <summary>
        /// Gets the underlying WMI object
        /// </summary>
        public ManagementBaseObject ActualObject => _model.ActualObject;

        /// <summary>
        /// Gets the class description from its qualifiers
        /// </summary>
        public string Description
        {
            get
            {
                if (_description == null)
                {
                    _description = _model.Description;
                }
                return _description;
            }
            private set => SetProperty(ref _description, value);
        }        

        #endregion

        #region ViewModel Properties

        public ObservableCollection<WmiInstancesViewModel> Instances
        {
            get => _instances;
            set => SetProperty(ref _instances, value);
        }

        // The filtered view of instances
        public ICollectionView WmiInstancesView => _wmiInstancesView ?? (_wmiInstancesView = CollectionViewSource.GetDefaultView(Instances));

        public ICommand LoadInstancesCommand { get; }
        
        public ICommand CopyRelativePathCommand { get; }

        public string QuickFilterInstances
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

        public WmiInstancesViewModel? SelectedInstance
        {
            get => _selectedInstance;
            set
            {
                if (SetProperty(ref _selectedInstance, value) && value != null)
                {
                    // Publish the message to notify other components about the selection change
                    PublishMessage(new SelectedInstanceChangedMessage(value));
                }
            }
        }

        public InstanceLoadState LoadState
        {
            get => _loadState;
            set => SetProperty(ref _loadState, value);
        }

        /// <summary>
        /// Gets or sets the parent namespace view model
        /// </summary>
        public WmiNamespacesViewModel? ParentNamespace
        {
            get => _parentNamespace;
            set => SetProperty(ref _parentNamespace, value);
        }
        
        #endregion

        #region Methods

        /// <summary>
        /// Copies the relative path of this class to the clipboard
        /// </summary>
        private void CopyRelativePath(object? parameter)
        {
            _applicationService.CopyToClipboard(ClassPath);
            PublishSuccessState($"Copied path: {ClassPath}");
        }

        /// <summary>
        /// Handle selected instance changes
        /// </summary>
        private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
        {
            if (message?.InstanceViewModel == null)
                return;

            // If this class contains the selected instance, select it in this class
            if (Instances.Contains(message.InstanceViewModel) && _selectedInstance != message.InstanceViewModel)
            {
                SelectedInstance = message.InstanceViewModel;
            }
        }

        private void OnFilterTimerTick(object? sender, EventArgs e)
        {
            _filterTimer?.Stop();

            if (_quickFilterInstances != _pendingQuickFilter)
            {
                _quickFilterInstances = _pendingQuickFilter;
                _wmiInstancesView?.Refresh();
            }
        }

        // Filter predicate for quick filtering instances
        private bool QuickFilterInstancesPredicate(object item)
        {
            if (string.IsNullOrWhiteSpace(_quickFilterInstances))
                return true;

            if (item is WmiInstancesViewModel instanceVm)
            {
                if (_quickFilterInstances.Length == 1)
                {
                    // Fast path for single character
                    return instanceVm.InstanceName.IndexOf(_quickFilterInstances, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                // Use IndexOf for better performance
                return instanceVm.InstanceName.IndexOf(_quickFilterInstances, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        /// <summary>
        /// Force selection notification even when already selected
        /// </summary>
        public void ForceSelection()
        {
            // Always publish the message even if already selected
            PublishMessage(new SelectedClassChangedMessage(this));
        }

        /// <summary>
        /// Returns the string representation of the class
        /// </summary>
        public override string ToString() => _model.ClassName;

        /// <summary>
        /// Override to clean up additional resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();

                // Dispose the filter timer
                if (_filterTimer != null)
                {
                    _filterTimer.Stop();
                    _filterTimer.Tick -= OnFilterTimerTick;
                    _filterTimer = null;
                }
            }
            
            base.Dispose(disposing);
        }

        public async Task LoadInstancesAsync()
        {
            if (LoadState == InstanceLoadState.Loading)
                return;

            try
            {
                LoadState = InstanceLoadState.Loading;
                
                // Set busy cursor state when starting to load instances
                PublishBusyState($"Loading instances for {ClassName}");

                // Call the service directly instead of going through the model
                var wmiInstances = await _wmiService.GetInstancesAsync(
                    ParentNamespace?.ManagementScope ?? throw new InvalidOperationException("ParentNamespace is required for ManagementScope."),
                    ClassName,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Map ManagementObject to WmiInstance
                var instanceModels = wmiInstances.Select(mo => new WmiInstance(mo));
                // Use the factory method to create view models for all instances at once
                var instanceViewModels = WmiInstancesViewModel.CreateFromCollection(
                    instanceModels,
                    _wmiService,
                    MessageService!,
                    _applicationService);

                await RunOnUIThreadAsync(() =>
                {
                    // Clear the quick filter to show all instances
                    _quickFilterInstances = string.Empty;
                    _pendingQuickFilter = string.Empty;

                    Instances.Clear();
                    foreach (var vm in instanceViewModels)
                    {
                        // Set parent class reference - ParentNamespace is now derived from ParentClass
                        vm.ParentClass = this;
                        
                        Instances.Add(vm);
                    }

                    // Update the collection view after loading new instances
                    _wmiInstancesView = CollectionViewSource.GetDefaultView(Instances);
                    _wmiInstancesView.Filter = QuickFilterInstancesPredicate;

                    // Notify that the filter has been reset
                    OnPropertyChanged(nameof(QuickFilterInstances));
                    
                    return Task.CompletedTask;
                });

                LoadState = InstanceLoadState.Success;
                // Update application state to success only after all instances are processed
                PublishSuccessState($"Loaded {instanceViewModels.Count} instances for {ClassName}");
            }
            catch (OperationCanceledException)
            {
                LoadState = InstanceLoadState.Failed;
                PublishErrorState($"Loading instances for {ClassName} was canceled");
            }
            catch (Exception ex)
            {
                LoadState = InstanceLoadState.Failed;
                PublishErrorState($"Error loading instances for {ClassName}: {ex.Message}", ex);
            }
        }
        
        #endregion
    }
}