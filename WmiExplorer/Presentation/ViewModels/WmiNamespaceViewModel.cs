using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModelHelpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

/// <summary>
/// ViewModel for a WMI namespace, supports async loading, filtering, and selection.
/// </summary>
public class WmiNamespaceViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ObservableCollection<WmiNamespaceViewModel> _children = new();
    private readonly ObservableCollection<WmiClassViewModel> _classes = new();
    private readonly FilterHelper<WmiClassViewModel> _classFilterHelper;
    private ClassLoadState _classLoadState = ClassLoadState.Unknown;
    private readonly object _collectionLock = new();
    private string _computerName = string.Empty;
    private ICommand? _copyRelativePathCommand;
    private readonly CancellationTokenSource _cts = new();
    private ICommand? _expandCommand;
    private bool _hasLoadedChildren;
    private bool _isExpanded;
    private bool _isSelected;
    private ICommand? _loadClassesCommand;
    private ManagementScope? _managementScope;
    private NamespaceLoadState _namespaceLoadState = NamespaceLoadState.Unknown;
    private WmiNamespaceViewModel? _parentNamespaceViewModel;
    private WmiClassViewModel? _selectedClass;
    private readonly ISettingsService _settingsService;
    private readonly WmiNamespace _wmiNamespace;
    private readonly IWmiService _wmiService;

    public WmiNamespaceViewModel(
        WmiNamespace wmiNamespace,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService,
        ISettingsService settingsService,
        WmiNamespaceViewModel? parentNamespaceViewModel = null)
    {
        // All dependencies are required for correct operation and messaging.
        _wmiNamespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        InitializeMessaging(messagingService);

        // The collection view is used for filtering and sorting classes in the UI.
        _classFilterHelper = new FilterHelper<WmiClassViewModel>(
            _classes,
            ClassMatchesFilter
        );

        Children = new ReadOnlyObservableCollection<WmiNamespaceViewModel>(_children);
        Classes = new ReadOnlyObservableCollection<WmiClassViewModel>(_classes);

        // StrongSubscribe ensures message handlers are not garbage collected.
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
        StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChanged, true);

        // Subscribe to ShowSystemClassesChanged to refresh filter
        _settingsService.ShowSystemClassesChanged += (s, show) =>
        {
            _classFilterHelper.CollectionView.Refresh();
            PublishMessage(new ClassesFilteredMessage(this));
        };

        // Set parent namespace if provided
        ParentNamespaceViewModel = parentNamespaceViewModel;
    }

    /// <summary>
    /// Children namespaces (read-only).
    /// </summary>
    public ReadOnlyObservableCollection<WmiNamespaceViewModel> Children { get; }

    /// <summary>
    /// Classes in this namespace (read-only).
    /// </summary>
    public ReadOnlyObservableCollection<WmiClassViewModel> Classes { get; }

    public ICollectionView ClassesView => _classFilterHelper.CollectionView;

    public string ClassFilterText
    {
        get => _classFilterHelper.FilterText;
        set
        {
            if (_classFilterHelper.FilterText != value)
            {
                _classFilterHelper.FilterText = value;
                OnPropertyChanged();
                PublishMessage(new ClassesFilteredMessage(this));
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

    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    public ICommand CopyRelativePathCommand
    {
        get => _copyRelativePathCommand ?? (_copyRelativePathCommand = new RelayCommand(_ => CopyRelativePath()));
        set => _copyRelativePathCommand = value;
    }

    public ICommand ExpandCommand
    {
        get => _expandCommand ?? (_expandCommand = new AsyncRelayCommand(ExpandAsync));
        set => _expandCommand = value;
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

    public ICommand LoadClassesCommand
    {
        get => _loadClassesCommand ?? (_loadClassesCommand = new AsyncRelayCommand(LoadClassesAsync));
        set => _loadClassesCommand = value;
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

    /// <summary>
    /// Lazily create and cache the ManagementScope for WMI operations.
    /// </summary>
    public ManagementScope ManagementScope
    {
        get
        {
            if (_managementScope == null)
            {
                var options = _wmiNamespace.ConnectionOptions;
                var scopePath = _wmiNamespace.NamespacePath;
                _managementScope = _wmiService.CreateManagementScope(scopePath, options);
            }
            return _managementScope;
        }
    }

    public string Name => _wmiNamespace.IsRoot ? _wmiNamespace.NamespacePath : _wmiNamespace.NamespaceName;

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

    public string NamespacePath => _wmiNamespace.NamespacePath;

    public WmiNamespaceViewModel? ParentNamespaceViewModel
    {
        get => _parentNamespaceViewModel;
        set => SetProperty(ref _parentNamespaceViewModel, value);
    }

    public WmiClassViewModel? SelectedClass
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

    public WmiNamespace? WmiNamespace => _wmiNamespace;

    public static ObservableCollection<WmiNamespaceViewModel> CreateFromCollection(
        IEnumerable<ManagementObject> mboCollection,
        WmiNamespace parentNamespaceModel,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService,
        ISettingsService settingsService,
        WmiNamespaceViewModel? parentNamespaceViewModel = null)
    {
        if (mboCollection == null)
            throw new ArgumentNullException(nameof(mboCollection));

        var viewModels = new ObservableCollection<WmiNamespaceViewModel>();

        foreach (var mo in mboCollection)
        {
            // Throws if the ManagementObject does not have a valid name or scope path.
            if (!(mo.Properties["Name"]?.Value is string name) || mo.Scope?.Path == null)
                throw new InvalidOperationException("Unable to determine child namespace path from ManagementObject.");

            string nsPath = $"{mo.Scope.Path.Path}\\{name}";
            var wmiNamespace = new WmiNamespace(mo, nsPath, parentNamespaceModel);

            var vm = new WmiNamespaceViewModel(
                wmiNamespace,
                wmiService,
                messagingService,
                applicationService,
                settingsService,
                parentNamespaceViewModel);

            if (mo.Scope?.Path != null)
                vm.ComputerName = mo.Scope.Path.Server;

            viewModels.Add(vm);
        }

        return viewModels;
    }

    public static async Task<WmiNamespaceViewModel> CreateRootAsync(
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
        if (rootMbo == null)
            throw new InvalidOperationException("Failed to retrieve the root WMI namespace object.");

        var rootNamespace = new WmiNamespace(rootMbo, namespacePath, new ConnectionOptions());
        var rootViewModel = new WmiNamespaceViewModel(
            rootNamespace,
            wmiService,
            messagingService,
            applicationService,
            settingsService);

        if (rootMbo?.Scope?.Path != null)
            rootViewModel.ComputerName = rootMbo.Scope.Path.Server;

        return rootViewModel;
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
                _wmiNamespace,
                _wmiService,
                MessageService!,
                _applicationService,
                _settingsService,
                this);

            var sortedChildViewModels = new ObservableCollection<WmiNamespaceViewModel>(
                childViewModels.OrderBy(vm => vm.Name)
            );

            await RunOnUIThreadAsync(() =>
            {
                lock (_collectionLock)
                {
                    _children.Clear();
                    foreach (var childViewModel in sortedChildViewModels)
                    {
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

    public void ForceSelection()
    {
        NotifyNamespaceSelected();
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
            var classViewModels = WmiClassViewModel.CreateFromCollection(
                classModels,
                this,
                _wmiService,
                MessageService!,
                _applicationService);

            await RunOnUIThreadAsync(() =>
            {
                lock (_collectionLock)
                {
                    _classes.Clear();
                    foreach (var classVm in classViewModels)
                    {
                        _classes.Add(classVm);
                    }
                }

                ClassesView.Refresh();
                return Task.CompletedTask;
            });

            ClassLoadState = ClassLoadState.Success;
            PublishSuccessState($"Loaded {ClassesView.Cast<object>().Count()} classes for {NamespacePath}");

            // Publish message that classes are loaded
            PublishMessage(new ClassesLoadedMessage(this));
        }
        catch (OperationCanceledException)
        {
            ClassLoadState = ClassLoadState.Failed;
            PublishErrorState($"Loading classes for {NamespacePath} was canceled");
        }
        catch (Exception ex)
        {
            ClassLoadState = ClassLoadState.Failed;
            PublishErrorState($"Error loading classes for {NamespacePath}: {ex.Message}", ex);
        }
    }

    public override string ToString() => _wmiNamespace.NamespacePath;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
            _classFilterHelper.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CopyRelativePath()
    {
        if (string.IsNullOrEmpty(NamespacePath))
            return;

        _applicationService.CopyToClipboard(NamespacePath);
        PublishSuccessState($"Copied path: {NamespacePath}");
    }

    private void HandleClassTypeFilterChanged(ClassTypeFilterChangedMessage message)
    {
        if (message == null) return;

        // No more System flag logic here, just refresh and publish
        _classFilterHelper.CollectionView.Refresh();
        PublishMessage(new ClassesFilteredMessage(this));
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

    private void NotifyNamespaceSelected()
    {
        PublishMessage(new SelectedNamespaceChangedMessage(this));
    }

    private bool ClassMatchesFilter(WmiClassViewModel classVm, string filter)
    {
        bool isSystemClass = classVm.ClassName.StartsWith("__");
        if (isSystemClass && !_settingsService.ShowSystemClasses)
            return false;
        if (!string.IsNullOrWhiteSpace(filter))
            return classVm.ClassName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        return true;
    }
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

public enum NamespaceLoadState
{
    Unknown,
    Loading,
    Success,
    Failed
}