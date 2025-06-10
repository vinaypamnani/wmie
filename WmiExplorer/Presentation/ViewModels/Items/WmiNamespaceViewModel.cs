using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI namespace, supports async loading, filtering, and selection.
/// </summary>
public partial class WmiNamespaceViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly ObservableCollection<WmiNamespaceViewModel> _children = new();
    private readonly ObservableCollection<WmiClassViewModel> _classes = new();
    private readonly FilterHelper<WmiClassViewModel> _classFilterHelper;

    [ObservableProperty]
    private string _classFilterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadState))]
    private ClassLoadState _classLoadState = ClassLoadState.Unknown;

    private readonly object _collectionLock = new();

    [ObservableProperty]
    private string _computerName = string.Empty;

    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private bool _hasLoadedChildren;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    private ManagementScope? _managementScope;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadState))]
    private NamespaceLoadState _namespaceLoadState = NamespaceLoadState.Unknown;

    [ObservableProperty]
    private WmiNamespaceViewModel? _parentNamespaceViewModel;

    private QueryTabViewModel _queryTabViewModel;
    private SearchTabViewModel _searchTabViewModel;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    private readonly ISettingsService _settingsService;
    private readonly WmiNamespace _wmiNamespace;
    private readonly IWmiService _wmiService;

    public WmiNamespaceViewModel(
           WmiNamespace wmiNamespace,
           IWmiService wmiService,
           IMessengerService messengerService,
           IApplicationService applicationService,
           ISettingsService settingsService,
           ICacheService cacheService,
           WmiNamespaceViewModel? parentNamespaceViewModel = null) : base(messengerService)
    {
        // All dependencies are required for correct operation and messaging.
        _wmiNamespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // The collection view is used for filtering and sorting classes in the UI.
        _classFilterHelper = new FilterHelper<WmiClassViewModel>(
            _classes,
            ClassFilterPredicate
        );

        Children = new ReadOnlyObservableCollection<WmiNamespaceViewModel>(_children);
        Classes = new ReadOnlyObservableCollection<WmiClassViewModel>(_classes);

        // StrongSubscribe ensures message handlers are not garbage collected.
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);

        // Subscribe to ShowSystemClassesChanged to refresh filter
        _settingsService.ShowSystemClassesChanged += (s, show) =>
        {
            _classFilterHelper.CollectionView.Refresh();
            PublishMessage(new ClassesFilteredMessage(this));
        };

        // Set parent namespace if provided
        ParentNamespaceViewModel = parentNamespaceViewModel;

        // Initialize query and search view models using DI - transient.
        _searchTabViewModel = App.ServiceProvider?.GetRequiredService<SearchTabViewModel>() ??
            throw new InvalidOperationException("Failed to resolve WmiSearchViewModel from service provider");
        _queryTabViewModel = App.ServiceProvider?.GetRequiredService<QueryTabViewModel>() ??
            throw new InvalidOperationException("Failed to resolve QueryTabViewModel from service provider");
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

    public LoadState LoadState
    {
        get
        {
            if (NamespaceLoadState == NamespaceLoadState.Loading || ClassLoadState == ClassLoadState.Loading)
                return LoadState.Loading;
            if (NamespaceLoadState == NamespaceLoadState.Failed || ClassLoadState == ClassLoadState.Failed)
                return LoadState.Failed;
            if (ClassLoadState == ClassLoadState.Warning)
                return LoadState.Warning;
            if (NamespaceLoadState == NamespaceLoadState.Success && ClassLoadState == ClassLoadState.Success)
                return LoadState.Success;
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
    public string NamespacePath => _wmiNamespace.NamespacePath;
    public QueryTabViewModel QueryTabViewModel => _queryTabViewModel;
    public SearchTabViewModel SearchTabViewModel => _searchTabViewModel;
    public WmiNamespace? WmiNamespace => _wmiNamespace;

    public static ObservableCollection<WmiNamespaceViewModel> CreateFromCollection(
           IEnumerable<ManagementObject> mboCollection,
           WmiNamespace parentNamespaceModel,
           IWmiService wmiService,
           IMessengerService messengerService,
           IApplicationService applicationService,
           ISettingsService settingsService,
           ICacheService cacheService,
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
            var wmiNamespace = new WmiNamespace(mo, nsPath, parentNamespaceModel); var vm = new WmiNamespaceViewModel(
                wmiNamespace,
                wmiService,
                messengerService,
                applicationService,
                settingsService,
                cacheService,
                parentNamespaceViewModel);
            if (mo.Scope?.Path != null)
                vm.ComputerName = mo.Scope.Path.Server;

            viewModels.Add(vm);
        }

        return viewModels;
    }

    public static async Task<WmiNamespaceViewModel> CreateRootAsync(
        string namespacePath, IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        ISettingsService settingsService,
        ICacheService cacheService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(namespacePath))
            throw new ArgumentException("Namespace path cannot be empty", nameof(namespacePath));

        var rootMbo = await wmiService.GetRootNamespaceAsync(namespacePath, cancellationToken);
        if (rootMbo == null)
            throw new InvalidOperationException("Failed to retrieve the root WMI namespace object.");

        var rootNamespace = new WmiNamespace(rootMbo, namespacePath, new ConnectionOptions()); var rootViewModel = new WmiNamespaceViewModel(
            rootNamespace,
            wmiService,
            messengerService,
            applicationService,
            settingsService,
            cacheService);

        if (rootMbo?.Scope?.Path != null)
            rootViewModel.ComputerName = rootMbo.Scope.Path.Server;
        return rootViewModel;
    }

    [RelayCommand]
    public async Task ExpandAsync()
    {
        if (HasLoadedChildren)
        {
            IsExpanded = true;
            return;
        }

        using var timer = OperationTimer.Start($"Loading child namespaces for {NamespacePath}", _messengerService);
        try
        {
            PublishBusyState($"Loading {NamespacePath}...");
            NamespaceLoadState = NamespaceLoadState.Loading;

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
                _messengerService,
                _applicationService,
                _settingsService,
                _cacheService,
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

    [RelayCommand]
    public async Task LoadClassesAsync()
    {
        using var timer = OperationTimer.Start($"Loading classes for {NamespacePath}", _messengerService);
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
                _messengerService,
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

    private bool ClassFilterPredicate(WmiClassViewModel classVm, string filter)
    {
        bool isSystemClass = classVm.ClassName.StartsWith("__");
        if (isSystemClass && !_settingsService.ShowSystemClasses)
            return false;
        if (!string.IsNullOrWhiteSpace(filter))
            return classVm.ClassName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        return true;
    }

    // Commands
    [RelayCommand]
    private void CopyRelativePath()
    {
        if (string.IsNullOrEmpty(NamespacePath))
            return;

        _applicationService.CopyToClipboard(NamespacePath);
        PublishSuccessState($"Copied path: {NamespacePath}");
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

    partial void OnClassFilterTextChanged(string value)
    {
        _classFilterHelper.FilterText = value;
        PublishMessage(new ClassesFilteredMessage(this));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            ExpandAsync().ConfigureAwait(false);
        }
    }

    // Property change notification methods
    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            // Expand the namespace if it is not already expanded
            if (!IsExpanded)
                IsExpanded = true;

            NotifyNamespaceSelected();
        }
    }
}

public enum ClassLoadState
{
    Unknown,
    Loading,
    Warning,
    Success,
    Failed
}

public enum LoadState
{
    Unknown,
    Loading,
    Success,
    Warning,
    Failed
}

public enum NamespaceLoadState
{
    Unknown,
    Loading,
    Success, Failed
}