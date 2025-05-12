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
    public enum NamespaceLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    public enum ClassLoadState
    {
        Unknown,
        Loading,
        Success,
        Failed
    }

    public enum LoadState
    {
        Unknown,
        Loading,
        Success,
        PartialSuccess,
        Failed
    }

    /// <summary>
    /// ViewModel for a WMI namespace, supports async loading, filtering, and selection.
    /// </summary>
    public class WmiNamespacesViewModel : MessagingViewModelBase
    {
        private readonly WmiNamespace _model;
        private readonly IWmiService _wmiService;
        private readonly IApplicationService _applicationService;
        private readonly ISettingsService _settingsService;
        private readonly ObservableCollection<WmiNamespacesViewModel> _children = new();
        private readonly ObservableCollection<WmiClassesViewModel> _classes = new();
        private readonly DebounceDispatcher _debouncer = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly object _collectionLock = new();
        private WmiNamespacesViewModel? _parentNamespace;
        private bool _hasLoadedChildren;
        private bool _isExpanded;
        private bool _isSelected;
        private ICollectionView? _classesView;
        private WmiClassesViewModel? _selectedClass;
        private string _quickFilterClasses = string.Empty;
        private string _pendingQuickFilter = string.Empty;
        private NamespaceLoadState _namespaceLoadState = NamespaceLoadState.Unknown;
        private ClassLoadState _classLoadState = ClassLoadState.Unknown;
        private ManagementScope? _managementScope;
        private ICommand? _loadClassesCommand;
        private ICommand? _expandCommand;
        private ICommand? _copyRelativePathCommand;
        private string _computerName = string.Empty;

        /// <summary>
        /// Children namespaces (read-only).
        /// </summary>
        public ReadOnlyObservableCollection<WmiNamespacesViewModel> Children { get; }
        /// <summary>
        /// Classes in this namespace (read-only).
        /// </summary>
        public ReadOnlyObservableCollection<WmiClassesViewModel> Classes { get; }

        public WmiNamespacesViewModel(
            WmiNamespace model,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            ISettingsService settingsService)
        {
            // All dependencies are required for correct operation and messaging.
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            InitializeMessaging(messagingService);

            // The collection view is used for filtering and sorting classes in the UI.
            _classesView = CollectionViewSource.GetDefaultView(_classes);
            _classesView.Filter = QuickFilterClassesPredicate;

            Children = new ReadOnlyObservableCollection<WmiNamespacesViewModel>(_children);
            Classes = new ReadOnlyObservableCollection<WmiClassesViewModel>(_classes);

            // StrongSubscribe ensures message handlers are not garbage collected.
            StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
            StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChanged, true);
        }

        public static ObservableCollection<WmiNamespacesViewModel> CreateFromCollection(
            IEnumerable<ManagementObject> mboCollection,
            WmiNamespace parent,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            ISettingsService settingsService)
        {
            if (mboCollection == null)
                throw new ArgumentNullException(nameof(mboCollection));

            var viewModels = new ObservableCollection<WmiNamespacesViewModel>();

            foreach (var mo in mboCollection)
            {
                // Throws if the ManagementObject does not have a valid name or scope path.
                if (!(mo.Properties["Name"]?.Value is string name) || mo.Scope?.Path == null)
                    throw new InvalidOperationException("Unable to determine child namespace path from ManagementObject.");
                string nsPath = $"{mo.Scope.Path.Path}\\{name}";
                var model = new WmiNamespace(mo, nsPath, parent);
                var vm = new WmiNamespacesViewModel(
                    model,
                    wmiService,
                    messagingService,
                    applicationService,
                    settingsService);
                if (mo.Scope?.Path != null)
                    vm.ComputerName = mo.Scope.Path.Server;
                viewModels.Add(vm);
            }

            return viewModels;
        }

        public string NamespacePath => _model.NamespacePath;
        public ManagementObject? ActualObject => _model.ActualObject;
        public string Name => _model.IsRoot ? _model.NamespacePath : _model.NamespaceName;

        public string ComputerName
        {
            get => _computerName;
            set => SetProperty(ref _computerName, value);
        }

        /// <summary>
        /// Lazily create and cache the ManagementScope for WMI operations.
        /// </summary>
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

        public bool HasLoadedChildren
        {
            get => _hasLoadedChildren;
            set => SetProperty(ref _hasLoadedChildren, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value)
                {
                    ExpandCommand?.Execute(null);
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && value)
                {
                    NotifyNamespaceSelected();
                }
            }
        }

        public NamespaceLoadState NamespaceLoadState
        {
            get => _namespaceLoadState;
            set
            {
                if (SetProperty(ref _namespaceLoadState, value))
                {
                    OnPropertyChanged(nameof(LoadState));
                }
            }
        }

        public ClassLoadState ClassLoadState
        {
            get => _classLoadState;
            set
            {
                if (SetProperty(ref _classLoadState, value))
                {
                    OnPropertyChanged(nameof(LoadState));
                }
            }
        }

        public ICollectionView ClassesView => _classesView ?? (_classesView = CollectionViewSource.GetDefaultView(Classes));

        public WmiClassesViewModel? SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetProperty(ref _selectedClass, value) && value != null)
                {
                    PublishMessage(new SelectedClassChangedMessage(value));
                }
            }
        }

        public string QuickFilterClasses
        {
            get => _pendingQuickFilter;
            set
            {
                if (SetProperty(ref _pendingQuickFilter, value))
                {
                    _debouncer.Debounce(() =>
                    {
                        if (_quickFilterClasses != _pendingQuickFilter)
                        {
                            _quickFilterClasses = _pendingQuickFilter;
                            _classesView?.Refresh();
                        }
                    });
                }
            }
        }

        public WmiNamespacesViewModel? ParentNamespace
        {
            get => _parentNamespace;
            set => SetProperty(ref _parentNamespace, value);
        }

        public LoadState LoadState
        {
            get
            {
                if (_namespaceLoadState == NamespaceLoadState.Loading || _classLoadState == ClassLoadState.Loading)
                    return LoadState.Loading;

                if (_namespaceLoadState == NamespaceLoadState.Success && _classLoadState == ClassLoadState.Success)
                    return LoadState.Success;

                if ((_namespaceLoadState == NamespaceLoadState.Success || _classLoadState == ClassLoadState.Success)
                    && _namespaceLoadState != NamespaceLoadState.Failed
                    && _classLoadState != ClassLoadState.Failed)
                    return LoadState.PartialSuccess;

                if (_namespaceLoadState == NamespaceLoadState.Failed || _classLoadState == ClassLoadState.Failed)
                    return LoadState.Failed;

                return LoadState.Unknown;
            }
        }

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

            var rootMbo = await wmiService.GetRootNamespaceAsync(namespacePath, cancellationToken);
            var rootModel = new WmiNamespace(rootMbo, namespacePath, new ConnectionOptions());
            var rootViewModel = new WmiNamespacesViewModel(
                rootModel,
                wmiService,
                messagingService,
                applicationService,
                settingsService);

            if (rootMbo?.Scope?.Path != null)
                rootViewModel.ComputerName = rootMbo.Scope.Path.Server;

            return rootViewModel;
        }

        private void NotifyNamespaceSelected()
        {
            PublishMessage(new SelectedNamespaceChangedMessage(this));
        }

        public void ForceSelection()
        {
            NotifyNamespaceSelected();
        }

        private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
        {
            if (message?.ClassViewModel == null) return;

            if (Classes.Contains(message.ClassViewModel) &&
                SelectedClass != message.ClassViewModel)
            {
                SelectedClass = message.ClassViewModel;
            }
        }

        private void HandleClassTypeFilterChanged(ClassTypeFilterChangedMessage message)
        {
            if (message == null) return;

            if (_classLoadState != ClassLoadState.Unknown && _isSelected)
            {
                System.Diagnostics.Debug.WriteLine($"WmiNamespacesViewModel ({NamespacePath}) received ClassTypeFilterChanged: {message.ClassTypeFilter}");

                _classes.Clear();
                _ = LoadClassesAsync();
            }
        }

        private void CopyRelativePath()
        {
            if (string.IsNullOrEmpty(NamespacePath))
                return;

            _applicationService.CopyToClipboard(NamespacePath);
            PublishSuccessState($"Copied path: {NamespacePath}");
        }

        private bool QuickFilterClassesPredicate(object item)
        {
            // Predicate for filtering classes by quick filter text (case-insensitive substring match).
            if (string.IsNullOrWhiteSpace(_quickFilterClasses))
                return true;

            if (item is WmiClassesViewModel classVm)
            {
                return classVm.ClassName.IndexOf(_quickFilterClasses, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

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
                PublishBusyState($"Loading {NamespacePath}...");

                // Use the ViewModel's ManagementScope for the service call.
                var childNamespaces = await _wmiService.GetChildNamespacesAsync(
                    ManagementScope,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Factory method creates child view models and sets up parent references.
                var childViewModels = CreateFromCollection(
                    childNamespaces,
                    _model,
                    _wmiService,
                    MessageService!,
                    _applicationService,
                    _settingsService);

                await RunOnUIThreadAsync(() =>
                {
                    lock (_collectionLock)
                    {
                        _children.Clear();
                        foreach (var childViewModel in childViewModels)
                        {
                            childViewModel.ParentNamespace = this;
                            _children.Add(childViewModel);
                        }
                    }
                    return Task.CompletedTask;
                });

                HasLoadedChildren = true;
                IsExpanded = true;
                NamespaceLoadState = NamespaceLoadState.Success;
                PublishSuccessState($"Loaded children for {NamespacePath}");
            }
            catch (OperationCanceledException)
            {
                NamespaceLoadState = NamespaceLoadState.Failed;
                PublishErrorState($"Loading {NamespacePath} was canceled");
            }
            catch (Exception ex)
            {
                NamespaceLoadState = NamespaceLoadState.Failed;
                PublishErrorState($"Error loading {NamespacePath}: {ex.Message}", ex);
            }
        }

        public async Task LoadClassesAsync()
        {
            try
            {
                ClassLoadState = ClassLoadState.Loading;
                PublishBusyState($"Loading classes for {NamespacePath}...");

                var classTypeFilter = _settingsService.ClassTypeFilter;

                // Use the ViewModel's ManagementScope for the service call.
                var wmiClasses = await _wmiService.GetClassesAsync(
                    ManagementScope,
                    classTypeFilter,
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                    return;

                // Map ManagementObject to WmiClass and create view models for all classes at once.
                var classModels = wmiClasses.Select(mo => new WmiClass(mo));
                var classViewModels = WmiClassesViewModel.CreateFromCollection(
                    classModels,
                    this,
                    _wmiService,
                    MessageService!,
                    _applicationService);

                var sortedClassViewModels = new ObservableCollection<WmiClassesViewModel>(
                    classViewModels.OrderBy(vm => vm.ClassName)
                );

                await RunOnUIThreadAsync(() =>
                {
                    lock (_collectionLock)
                    {
                        _classes.Clear();
                        foreach (var classVm in sortedClassViewModels)
                        {
                            _classes.Add(classVm);
                        }
                    }

                    ClassesView.Refresh();
                    return Task.CompletedTask;
                });

                ClassLoadState = ClassLoadState.Success;
                PublishSuccessState($"Loaded {sortedClassViewModels.Count} classes for {NamespacePath}");
            }
            catch (OperationCanceledException)
            {
                ClassLoadState = ClassLoadState.Failed;
                PublishErrorState($"Loading classes for {NamespacePath} was canceled");
            }
            catch (Exception ex)
            {
                ClassLoadState = ClassLoadState.Failed;
                PublishErrorState($"Error loading {NamespacePath}: {ex.Message}", ex);
            }
        }

        public override string ToString() => _model.ToString();

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
    }
}