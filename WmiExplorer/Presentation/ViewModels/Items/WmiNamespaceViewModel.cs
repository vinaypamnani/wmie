using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Models;
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
    private WmiNamespaceViewModel? _parentNamespaceViewModel;

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    private readonly SelectionManager _selectionManager;
    private readonly SettingsManager _settingsManager;
    private readonly ISettingsService? _settingsService;

    /// <summary>
    /// Gets the count of system classes (class names starting with "__") in this namespace.
    /// </summary>
    [ObservableProperty]
    private int _systemClassesCount;

    private readonly WmiNamespace _wmiNamespace;
    private readonly IWmiService _wmiService;

    [ObservableProperty]
    private ItemStatus itemStatus = new();

    public WmiNamespaceViewModel(
           WmiNamespace wmiNamespace,
           IWmiService wmiService,
           IMessengerService messengerService,
           IApplicationService applicationService,
           SettingsManager settingsManager,
           ICacheService cacheService,
           SelectionManager selectionManager,
           WmiNamespaceViewModel? parentNamespaceViewModel = null,
           ISettingsService? settingsService = null) : base(messengerService)
    {
        // All dependencies are required for correct operation and messaging.
        _wmiNamespace = wmiNamespace ?? throw new ArgumentNullException(nameof(wmiNamespace));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));
        _settingsService = settingsService;

        // Subscribe to ItemStatus property changes to notify Tooltip changes
        ItemStatus.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ItemStatus.LoadState) ||
                e.PropertyName == nameof(ItemStatus.StatusMessage) ||
                e.PropertyName == nameof(ItemStatus.Exception))
            {
                OnPropertyChanged(nameof(Tooltip));
            }
        };

        // Initialize filter helper for classes
        _classFilterHelper = new FilterHelper<WmiClassViewModel>(_classes, ClassFilterPredicate);

        // Set up read-only collections
        Children = new ReadOnlyObservableCollection<WmiNamespaceViewModel>(_children);
        Classes = new ReadOnlyObservableCollection<WmiClassViewModel>(_classes);

        // Set parent namespace
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

    /// <summary>
    /// Lazily create and cache the ManagementScope for WMI operations.
    /// </summary>
    public ManagementScope ManagementScope
    {
        get
        {
            if (_managementScope == null)
            {
                _managementScope = _wmiNamespace.CreateManagementScope();
                _wmiNamespace.IsConnected = true;
            }
            return _managementScope;
        }
    }

    public string Name => _wmiNamespace.IsRoot ? _wmiNamespace.NamespacePath : _wmiNamespace.NamespaceName;
    public string NamespacePath => _wmiNamespace.NamespacePath;

    public string? Tooltip
    {
        get
        {
            switch (ItemStatus.LoadState)
            {
                case LoadState.Unknown:
                    return null;
                case LoadState.Loading:
                    return "Loading";
                case LoadState.Expanding:
                    return "Expanding namespace";
                case LoadState.PartialSuccess:
                    return GetContextualTooltip($"Expanded [{Children.Count} child namespaces]. Double click to enumerate classes.");
                case LoadState.Success:
                    return GetContextualTooltip($"Success [{Classes?.Count ?? 0} classes]");
                case LoadState.Warning:
                    return !string.IsNullOrWhiteSpace(ItemStatus.StatusMessage) ? ItemStatus.StatusMessage : "Warning";
                case LoadState.Error:
                    return ItemStatus.Exception?.Message ?? "Failed";
                default:
                    return null;
            }
        }
    }

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
           WmiNamespaceViewModel? parentNamespaceViewModel = null,
           ISettingsService? settingsService = null)
    {
        if (mboCollection == null)
            throw new ArgumentNullException(nameof(mboCollection));

        var viewModels = new ObservableCollection<WmiNamespaceViewModel>();

        foreach (var mo in mboCollection)
        {
            if (!(mo.Properties["Name"]?.Value is string name) || mo.Scope?.Path == null)
                throw new InvalidOperationException("Unable to determine child namespace path from ManagementObject.");

            string nsPath = $"{mo.Scope.Path.Path}\\{name}";
            var wmiNamespace = new WmiNamespace(mo, nsPath, parentNamespaceModel, wmiService);
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
                    parentNamespaceViewModel,
                    settingsService);
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
                    parentNamespaceViewModel,
                    settingsService);
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
                    parentNamespaceViewModel,
                    settingsService);
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
        CancellationToken cancellationToken = default,
        ISettingsService? settingsService = null)
    {
        if (string.IsNullOrEmpty(namespacePath))
            throw new ArgumentException("Namespace path cannot be empty", nameof(namespacePath));

        var rootMbo = await wmiService.GetRootNamespaceAsync(namespacePath, connectionOptions, cancellationToken);
        var rootNamespace = new WmiNamespace(rootMbo!, namespacePath, connectionOptions, wmiService);
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
                selectionManager,
                null,
                settingsService);
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
                selectionManager,
                null,
                settingsService);
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
                selectionManager,
                null,
                settingsService);
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
            // Don't set IsExpanded to true here, we are here because it was set to true.
            return;
        }

        using var timer = OperationTimer.Start($"Loading child namespaces for {NamespacePath}", _messengerService);

        try
        {
            SetStatusAndPublish(ItemStatus, LoadState.Expanding, $"Loading child namespaces for {NamespacePath}...");
            Log.Debug("Loading child namespaces for {NamespacePath}", NamespacePath);

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
                this,
                _settingsService);

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
            SetStatusAndPublish(ItemStatus, LoadState.PartialSuccess, $"Loaded [{_children.Count}] child namespaces for {NamespacePath}. Double click to enumerate classes.");
            Log.Information("Successfully loaded {ChildCount} child namespaces for {NamespacePath}", _children.Count, NamespacePath);
        }
        catch (OperationCanceledException)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Warning, $"Loading child namespaces for {NamespacePath} was canceled");
            Log.Warning("Loading child namespaces for {NamespacePath} was canceled", NamespacePath);
        }
        catch (Exception ex)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Error, $"Error loading child namespaces for {NamespacePath}: {ex.Message}", ex);
            Log.Error(ex, "Error loading child namespaces for {NamespacePath}", NamespacePath);
        }
    }

    [RelayCommand]
    public async Task LoadClassesAsync()
    {
        Log.Debug("Loading classes for {NamespacePath}", NamespacePath);
        try
        {
            SetStatusAndPublish(ItemStatus, LoadState.Loading, $"Loading classes for {NamespacePath}...");

            var queryString = BuildClassQueryFromFilter(_settingsManager.ClassEnumerationFilter);
            if (IsSmsProviderNamespace && this is ConfigMgr.SmsProviderNamespaceViewModel smsVm)
            {
                queryString = ConfigMgr.SmsProviderNamespaceViewModel.BuildSmsProviderQueryFromFilter(queryString, _settingsManager.ConfigMgrSettings!);
            }

            var wmiClasses = await _wmiService.ExecuteWmiQueryAsync(
                ManagementScope,
                queryString,
                directRead: false,
                useAmendedQualifiers: true,
                cacheResults: true,
                enableLogging: false,
                _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                Log.Warning("Loading classes for {NamespacePath} was canceled (token signaled)", NamespacePath);
                return;
            }

            // Materialize to list to avoid multiple enumerations
            var classModels = wmiClasses.Select(mo => new WmiClass(mo, _wmiService)).ToList();

            // Count classes with providers for logging
            var classesWithProviders = classModels.Count(c => c.Provider != null);
            Log.Debug("Loaded {ClassCount} classes with {ProviderCount} providers for {NamespacePath}", classModels.Count, classesWithProviders, NamespacePath);

            var classViewModels = WmiClassViewModel.CreateFromCollection(
                classModels,
                this,
                _wmiService,
                _messengerService,
                _applicationService,
                _selectionManager,
                _settingsService);

            // Use RunOnUIThreadAsync for asynchronous UI updates to avoid hanging
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

                UpdateSystemClassesCount();
                // Let FilterHelper handle collection view updates
                OnPropertyChanged(nameof(ClassFilterText));
                return Task.CompletedTask;
            });

            SetStatusAndPublish(ItemStatus, LoadState.Success, $"Successfully loaded {_classes.Count} classes for {NamespacePath}");
            UpdateFilterStatusMessage(); // Update the status bar message with "Showing" message which accounts for system class filtering.
            Log.Information("Successfully loaded {ClassCount} classes for {NamespacePath}", _classes.Count, NamespacePath);

            PublishMessage(new ClassesLoadedMessage(this));
        }
        catch (OperationCanceledException ocex)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Warning, $"Loading classes for {NamespacePath} was canceled");
            Log.Warning(ocex, "Loading classes for {NamespacePath} was canceled (exception)", NamespacePath);
        }
        catch (Exception ex)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Error, $"Error loading classes for {NamespacePath}: {ex.Message}", ex);
            Log.Error(ex, "Error loading classes for {NamespacePath}", NamespacePath);
        }
    }

    /// <summary>
    /// Call this when ShowSystemClasses changes to refresh the filter view.
    /// </summary>
    public void OnShowSystemClassesChanged()
    {
        _classFilterHelper.CollectionView.Refresh();
        if (IsSelected)
        {
            UpdateFilterStatusMessage();
        }
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

            // Clear provider cache for this namespace when disposing
            if (!string.IsNullOrEmpty(NamespacePath))
            {
                _wmiService.ClearProviderCache(NamespacePath);
            }
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
        {
            UpdateSystemClassesCount();
            return;
        }

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

        UpdateSystemClassesCount();
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

    /// <summary>
    /// Generates contextual tooltip text based on the namespace type and properties.
    /// </summary>
    /// <param name="baseTooltip">The base tooltip text to enhance with context.</param>
    /// <returns>Enhanced tooltip text with contextual information.</returns>
    private string GetContextualTooltip(string baseTooltip)
    {
        if (IsSmsClientNamespace)
        {
            return $"{baseTooltip} [ConfigMgr Client Namespace. Right click this namespace to see extra options to trigger common client actions]";
        }
        else if (IsSmsProviderNamespace)
        {
            return $"{baseTooltip} [ConfigMgr Provider Namespace. Use extra options in the Classes Tab to include/exclude Collection and Inventory classes]";
        }
        // If this is a root namespace, provide additional context
        else if (IsRoot)
        {
            return $"Root namespace for this connection. Right click to disconnect.";
        }
        return baseTooltip;
    }

    // Property change notification methods
    partial void OnClassFilterTextChanged(string value)
    {
        _classFilterHelper.SetFilterText(value, () =>
        {
            if (IsSelected)
                UpdateFilterStatusMessage();
        });
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

    /// <summary>
    /// Updates the status message to reflect the current filtering state
    /// </summary>
    private void UpdateFilterStatusMessage()
    {
        if (ItemStatus.LoadState != LoadState.Success)
            return;

        var totalCount = _classes.Count;
        var filteredCount = ClassesView.Cast<object>().Count();

        string statusMessage;
        if (string.IsNullOrWhiteSpace(ClassFilterText))
        {
            if (totalCount == filteredCount)
                statusMessage = $"Showing {totalCount} classes for {NamespacePath}";
            else
                statusMessage = $"Showing {filteredCount} of {totalCount} classes for {NamespacePath}. Toggle System Classes to see all classes.";
        }
        else
        {
            statusMessage = $"Filtered {filteredCount} of {totalCount} classes for {NamespacePath} matching '{ClassFilterText}'";
        }

        SetStatusAndPublish(ItemStatus, LoadState.Success, statusMessage);
    }

    /// <summary>
    /// Updates the SystemClassesCount property based on the current Classes collection.
    /// </summary>
    private void UpdateSystemClassesCount()
    {
        lock (_collectionLock)
        {
            SystemClassesCount = _classes.Count(c => c.ClassName != null && c.ClassName.StartsWith("__"));
        }
    }
}