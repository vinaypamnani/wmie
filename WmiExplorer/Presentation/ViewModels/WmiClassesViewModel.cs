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
    public enum InstanceLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    /// <summary>
    /// ViewModel for a WMI class, supports async loading, filtering, and selection of instances.
    /// </summary>
    public class WmiClassesViewModel : MessagingViewModelBase
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IWmiService _wmiService;
        private readonly IApplicationService _applicationService;
        private readonly WmiClass _model;
        private readonly WmiNamespacesViewModel _parentNamespace;
        private readonly ObservableCollection<WmiInstancesViewModel> _instances = new();
        private readonly DebounceDispatcher _debouncer = new();
        private readonly object _collectionLock = new();
        private ICollectionView? _wmiInstancesView;
        private string _pendingQuickFilter = string.Empty;
        private string _quickFilterInstances = string.Empty;
        private WmiInstancesViewModel? _selectedInstance;
        private InstanceLoadState _loadState = InstanceLoadState.Unknown;

        /// <summary>
        /// Instances of this class (read-only).
        /// </summary>
        public ReadOnlyObservableCollection<WmiInstancesViewModel> Instances { get; }

        public WmiClassesViewModel(
            WmiClass model,
            WmiNamespacesViewModel parentNamespace,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            // All dependencies are required for correct operation and messaging.
            _model = model;
            _wmiService = wmiService;
            _applicationService = applicationService;
            _parentNamespace = parentNamespace ?? throw new ArgumentNullException(nameof(parentNamespace));

            InitializeMessaging(messagingService);

            LoadInstancesCommand = new AsyncRelayCommand(LoadInstancesAsync);
            CopyRelativePathCommand = new RelayCommand(CopyRelativePath);

            // StrongSubscribe ensures message handlers are not garbage collected.
            StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);

            // The collection view is used for filtering and sorting instances in the UI.
            _wmiInstancesView = CollectionViewSource.GetDefaultView(_instances);
            _wmiInstancesView.Filter = QuickFilterInstancesPredicate;

            Instances = new ReadOnlyObservableCollection<WmiInstancesViewModel>(_instances);
        }

        public static ObservableCollection<WmiClassesViewModel> CreateFromCollection(
            IEnumerable<WmiClass> models,
            WmiNamespacesViewModel parentNamespace,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            var viewModels = new ObservableCollection<WmiClassesViewModel>();

            foreach (var model in models)
            {
                viewModels.Add(new WmiClassesViewModel(
                    model,
                    parentNamespace,
                    wmiService,
                    messagingService,
                    applicationService));
            }

            return viewModels;
        }

        public string ClassName => _model.ClassName;
        public string ClassPath => _model.ClassPath;
        public ManagementBaseObject ActualObject => _model.ActualObject;
        public string Description => _model.Description;

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
                    _debouncer.Debounce(() =>
                    {
                        if (_quickFilterInstances != _pendingQuickFilter)
                        {
                            _quickFilterInstances = _pendingQuickFilter;
                            _wmiInstancesView?.Refresh();
                        }
                    });
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
                    PublishMessage(new SelectedInstanceChangedMessage(value));
                }
            }
        }

        public InstanceLoadState LoadState
        {
            get => _loadState;
            set => SetProperty(ref _loadState, value);
        }

        public WmiNamespacesViewModel ParentNamespace => _parentNamespace;

        public ManagementScope ManagementScope => _parentNamespace.ManagementScope;

        private void CopyRelativePath(object? parameter)
        {
            _applicationService.CopyToClipboard(ClassPath);
            PublishSuccessState($"Copied path: {ClassPath}");
        }

        private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
        {
            if (message?.InstanceViewModel == null)
                return;

            if (_instances.Contains(message.InstanceViewModel) && _selectedInstance != message.InstanceViewModel)
            {
                SelectedInstance = message.InstanceViewModel;
            }
        }

        private bool QuickFilterInstancesPredicate(object item)
        {
            // Predicate for filtering instances by quick filter text (case-insensitive substring match).
            if (string.IsNullOrWhiteSpace(_quickFilterInstances))
                return true;

            if (item is WmiInstancesViewModel instanceVm)
            {
                return instanceVm.InstanceName.IndexOf(_quickFilterInstances, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        public void ForceSelection()
        {
            PublishMessage(new SelectedClassChangedMessage(this));
        }

        public override string ToString() => _model.ClassName;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _debouncer.Dispose();
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

                PublishBusyState($"Loading instances for {ClassName}");

                // Use the parent namespace's ManagementScope for the service call.
                var wmiInstances = await _wmiService.GetInstancesAsync(
                    ParentNamespace.ManagementScope,
                    ClassName,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Map ManagementObject to WmiInstance and create view models for all instances at once.
                var instanceModels = wmiInstances.Select(mo => new WmiInstance(mo));
                var instanceViewModels = WmiInstancesViewModel.CreateFromCollection(
                    instanceModels,
                    _wmiService,
                    MessageService!,
                    _applicationService,
                    this);

                await RunOnUIThreadAsync(() =>
                {
                    lock (_collectionLock)
                    {
                        _instances.Clear();
                        foreach (var vm in instanceViewModels)
                        {
                            _instances.Add(vm);
                        }
                    }

                    _wmiInstancesView = CollectionViewSource.GetDefaultView(Instances);
                    _wmiInstancesView.Filter = QuickFilterInstancesPredicate;

                    OnPropertyChanged(nameof(QuickFilterInstances));

                    return Task.CompletedTask;
                });

                LoadState = InstanceLoadState.Success;
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
    }
}