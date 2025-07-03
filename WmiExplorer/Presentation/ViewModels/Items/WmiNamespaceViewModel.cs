using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Shared;
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
    [NotifyPropertyChangedFor(nameof(ClassesView))]
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

    private bool _isUpdatingSelection = false;
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

    private readonly SelectionManager _selectionManager;
    private readonly SettingsManager _settingsManager;
    private readonly WmiNamespace _wmiNamespace;
    private readonly IWmiService _wmiService;

    public WmiNamespaceViewModel(
           WmiNamespace wmiNamespace,
           IWmiService wmiService,
           IMessengerService messengerService,
           IApplicationService applicationService,
           SettingsManager settingsManager,
           ICacheService cacheService,
           SelectionManager selectionManager,
           WmiNamespaceViewModel? parentNamespaceViewModel = null) : base(messengerService)
    {
        // All dependencies are required for correct operation and messaging.
        _wmiNamespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));

        // The collection view is used for filtering and sorting classes in the UI.
        _classFilterHelper = new FilterHelper<WmiClassViewModel>(
            _classes,
            ClassFilterPredicate
        );

        Children = new ReadOnlyObservableCollection<WmiNamespaceViewModel>(_children);
        Classes = new ReadOnlyObservableCollection<WmiClassViewModel>(_classes);

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

    /// <summary>
    /// Indicates whether this namespace is a root namespace (can be disconnected)
    /// </summary>
    public bool IsRoot => _wmiNamespace.IsRoot;

    /// <summary>
    /// Indicates whether this namespace is an SMS Client namespace. Overridden in derived class.
    /// </summary>
    public virtual bool IsSmsClientNamespace => false;

    /// <summary>
    /// Indicates whether this namespace is an SMS Provider namespace. Overridden in derived class.
    /// </summary>
    public virtual bool IsSmsProviderNamespace => false;

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
                _wmiNamespace.IsConnected = true;
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
           SettingsManager settingsManager,
           ICacheService cacheService,
           SelectionManager selectionManager,
           WmiNamespaceViewModel? parentNamespaceViewModel = null)
    {
        if (mboCollection == null)
            throw new ArgumentNullException(nameof(mboCollection));

        var viewModels = new ObservableCollection<WmiNamespaceViewModel>();

        foreach (var mo in mboCollection)
        {
            if (!(mo.Properties["Name"]?.Value is string name) || mo.Scope?.Path == null)
                throw new InvalidOperationException("Unable to determine child namespace path from ManagementObject.");

            string nsPath = $"{mo.Scope.Path.Path}\\{name}";
            var wmiNamespace = new WmiNamespace(mo, nsPath, parentNamespaceModel);
            WmiNamespaceViewModel vm;
            // Debug.WriteLine($"Creating WmiNamespaceViewModel for path: {nsPath}");
            if (ConfigMgr.SmsClientNamespaceViewModel.IsSmsClientNamespacePath(wmiNamespace.RelativePath))
            {
                vm = new ConfigMgr.SmsClientNamespaceViewModel(
                    wmiNamespace,
                    wmiService,
                    messengerService,
                    applicationService,
                    settingsManager,
                    cacheService,
                    selectionManager,
                    parentNamespaceViewModel);
            }
            else if (ConfigMgr.SmsProviderNamespaceViewModel.IsSmsProviderNamespacePath(wmiNamespace.RelativePath))
            {
                vm = new ConfigMgr.SmsProviderNamespaceViewModel(
                    wmiNamespace,
                    wmiService,
                    messengerService,
                    applicationService,
                    settingsManager,
                    cacheService,
                    selectionManager,
                    parentNamespaceViewModel);
            }
            else
            {
                vm = new WmiNamespaceViewModel(
                    wmiNamespace,
                    wmiService,
                    messengerService,
                    applicationService,
                    settingsManager,
                    cacheService,
                    selectionManager,
                    parentNamespaceViewModel);
            }

            if (mo.Scope?.Path != null)
                vm.ComputerName = mo.Scope.Path.Server;

            viewModels.Add(vm);
        }

        return viewModels;
    }

    public static async Task<WmiNamespaceViewModel> CreateRootAsync(
        string namespacePath,
        ConnectionOptions connectionOptions,
        IWmiService wmiService,
        IMessengerService messengerService,
        IApplicationService applicationService,
        SettingsManager settingsManager,
        ICacheService cacheService,
        SelectionManager selectionManager,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(namespacePath))
            throw new ArgumentException("Namespace path cannot be empty", nameof(namespacePath));

        var rootMbo = await wmiService.GetRootNamespaceAsync(namespacePath, connectionOptions, cancellationToken);
        var rootNamespace = new WmiNamespace(rootMbo!, namespacePath, connectionOptions);
        WmiNamespaceViewModel rootViewModel;
        string nsPath = namespacePath;
        if (ConfigMgr.SmsClientNamespaceViewModel.IsSmsClientNamespacePath(rootNamespace.RelativePath))
        {
            rootViewModel = new ConfigMgr.SmsClientNamespaceViewModel(
                rootNamespace,
                wmiService,
                messengerService,
                applicationService,
                settingsManager,
                cacheService,
                selectionManager);
        }
        else if (ConfigMgr.SmsProviderNamespaceViewModel.IsSmsProviderNamespacePath(rootNamespace.RelativePath))
        {
            rootViewModel = new ConfigMgr.SmsProviderNamespaceViewModel(
                rootNamespace,
                wmiService,
                messengerService,
                applicationService,
                settingsManager,
                cacheService,
                selectionManager);
        }
        else
        {
            rootViewModel = new WmiNamespaceViewModel(
                rootNamespace,
                wmiService,
                messengerService,
                applicationService,
                settingsManager,
                cacheService,
                selectionManager);
        }

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
            PublishBusyState($"Loading child namespaces for {NamespacePath}...");
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
                _settingsManager,
                _cacheService,
                _selectionManager,
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
            PublishSuccessState($"Loaded child namespaces for {NamespacePath}");
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Loading child namespaces for {NamespacePath} was canceled", NamespacePath);
            NamespaceLoadState = NamespaceLoadState.Failed;
            PublishErrorState($"Loading child namespaces for {NamespacePath} was canceled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading child namespaces for {NamespacePath}", NamespacePath);
            NamespaceLoadState = NamespaceLoadState.Failed;
            PublishErrorState($"Error loading child namespaces for {NamespacePath}: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    public async Task LoadClassesAsync()
    {
        Log.Debug("Starting to load classes for {NamespacePath}", NamespacePath);
        using var timer = OperationTimer.Start($"Loading classes for {NamespacePath}", _messengerService);
        try
        {
            ClassLoadState = ClassLoadState.Loading;
            PublishBusyState($"Loading classes for {NamespacePath}...");

            var queryString = BuildClassQueryFromFilter(_settingsManager.ClassEnumerationFilter);
            if (IsSmsProviderNamespace && this is ConfigMgr.SmsProviderNamespaceViewModel smsVm)
            {
                // Use derived class method if available
                queryString = ConfigMgr.SmsProviderNamespaceViewModel.BuildSmsProviderQueryFromFilter(queryString, _settingsManager.ConfigMgrSettings!);
            }

            // Use the ViewModel's ManagementScope for the service call.
            var wmiClasses = await _wmiService.ExecuteWmiQueryAsync(
                ManagementScope,
                queryString,
                directRead: false,
                useAmendedQualifiers: true,
                cacheResults: true, // Ensure class metadata is cached for this namespace
                _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Loading classes for {NamespacePath} was canceled (token signaled)", NamespacePath);
                return;
            }

            // Map ManagementObject to WmiClass and create view models for all classes at once.
            var classModels = wmiClasses.Select(mo => new WmiClass(mo));
            var classViewModels = WmiClassViewModel.CreateFromCollection(
                classModels,
                this,
                _wmiService,
                _messengerService,
                _applicationService,
                _selectionManager);

            await RunOnUIThreadAsync(() =>
            {
                ClearAndDisposeClasses();
                lock (_collectionLock)
                {
                    foreach (var classVm in classViewModels)
                    {
                        _classes.Add(classVm);
                    }
                }

                ClassesView.Refresh();
                return Task.CompletedTask;
            });

            ClassLoadState = ClassLoadState.Success;
            Log.Information("Successfully loaded {ClassCount} classes for {NamespacePath}", _classes.Count, NamespacePath);

            // Publish message that classes are loaded
            PublishMessage(new ClassesLoadedMessage(this));

            // Publish message that tab count changed
            PublishMessage(new TabCountChangedMessage());

            // Publish message that classes are filtered to update status bar
            PublishMessage(new ClassesFilteredMessage(this));
        }
        catch (OperationCanceledException ocex)
        {
            ClassLoadState = ClassLoadState.Warning;
            Log.Warning(ocex, "Loading classes for {NamespacePath} was canceled (exception)", NamespacePath);
            PublishErrorState($"Loading classes for {NamespacePath} was canceled");
        }
        catch (Exception ex)
        {
            ClassLoadState = ClassLoadState.Failed;
            Log.Error(ex, "Error loading classes for {NamespacePath}", NamespacePath);
            PublishErrorState($"Error loading classes for {NamespacePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Call this when ShowSystemClasses changes to refresh the filter view.
    /// </summary>
    public void OnShowSystemClassesChanged()
    {
        _classFilterHelper.CollectionView.Refresh();
        if (IsSelected)
            PublishMessage(new ClassesFilteredMessage(this));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose all child namespaces
            ClearAndDisposeChildren();
            ClearAndDisposeClasses();
            _wmiNamespace?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
            _classFilterHelper.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Builds a WQL query string based on the class type filter
    /// </summary>
    private static string BuildClassQueryFromFilter(WmiClassEnumerationFlags classTypeFilter)
    {
        // For All filter, use the simplest possible query
        if (classTypeFilter == WmiClassEnumerationFlags.All)
            return "SELECT * FROM meta_class";

        // Start with a simple base query without the problematic LIKE '%'
        string query = "SELECT * FROM meta_class WHERE __Class LIKE '%'";

        // Remove System class filtering here; always return system classes
        // Only filter CIM, MSFT, Perf
        if ((classTypeFilter & WmiClassEnumerationFlags.CIM) != WmiClassEnumerationFlags.CIM)
            query += " AND NOT __Class LIKE \"CIM[_]%\"";

        if ((classTypeFilter & WmiClassEnumerationFlags.MSFT) != WmiClassEnumerationFlags.MSFT)
            query += " AND NOT __Class LIKE \"MSFT[_]%\"";

        if ((classTypeFilter & WmiClassEnumerationFlags.Perf) != WmiClassEnumerationFlags.Perf)
            query += " AND NOT __Class LIKE \"Win32_Perf%\"";

        return query;
    }

    private bool ClassFilterPredicate(WmiClassViewModel classVm, string filter)
    {
        bool isSystemClass = classVm.ClassName.StartsWith("__");
        if (isSystemClass && !_settingsManager.ShowSystemClasses)
            return false;
        if (!string.IsNullOrWhiteSpace(filter))
            return classVm.ClassName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        return true;
    }

    /// <summary>
    /// Disposes all WmiNamespaceViewModel items in the _children collection and clears the collection.
    /// </summary>
    private void ClearAndDisposeChildren()
    {
        List<WmiNamespaceViewModel> toDispose;
        lock (_collectionLock)
        {
            toDispose = new List<WmiNamespaceViewModel>(_children);
            _children.Clear();
        }
        foreach (var childNamespace in toDispose)
        {
            try
            {
                childNamespace.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing child namespace: {NamespacePath}", childNamespace.NamespacePath);
            }
        }
    }

    /// <summary>
    /// Disposes all WmiClassViewModel items in the _classes collection and clears the collection.
    /// </summary>
    private void ClearAndDisposeClasses()
    {
        if (_classes.Count == 0)
            return;

        List<WmiClassViewModel> toDispose;
        lock (_collectionLock)
        {
            toDispose = new List<WmiClassViewModel>(_classes);
            _classes.Clear();
        }
        foreach (var wmiClass in toDispose)
        {
            try
            {
                wmiClass.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing class: {ClassName}", wmiClass.ClassName);
            }
        }

        Log.Debug("Cleared and disposed all classes for namespace: {NamespacePath}", NamespacePath);
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

    /// <summary>
    /// Command to disconnect (remove) this root namespace from the tree
    /// </summary>
    [RelayCommand(CanExecute = nameof(DisconnectCanExecute))]
    private void Disconnect()
    {
        // Send message to NamespacesViewModel to handle the removal
        PublishMessage(new DisconnectNamespaceMessage(this));
    }

    /// <summary>
    /// Determines if the disconnect command can execute (only for root namespaces)
    /// </summary>
    private bool DisconnectCanExecute() => IsRoot;

    // Property change notification methods
    partial void OnClassFilterTextChanged(string value)
    {
        _classFilterHelper.FilterText = value;
        if (IsSelected)
            PublishMessage(new ClassesFilteredMessage(this));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            // Fire and forget - don't block the UI thread
            _ = ExpandAsync();
        }
    }

    // Property change notification methods
    partial void OnIsSelectedChanged(bool value)
    {
        if (_isUpdatingSelection) return;

        if (value)
        {
            try
            {
                _isUpdatingSelection = true;

                // Set expanded state immediately - the async expansion will happen in OnIsExpandedChanged
                if (!IsExpanded)
                    IsExpanded = true;
            }
            finally
            {
                _isUpdatingSelection = false;
            }
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
    Success,
    Failed
}