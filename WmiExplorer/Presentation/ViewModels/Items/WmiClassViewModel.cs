using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Items;

/// <summary>
/// ViewModel for a WMI class, supports async loading, filtering, and selection of instances.
/// </summary>
public partial class WmiClassViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly object _collectionLock = new();
    private CancellationTokenSource _cts = new();
    private bool _hasLazyProperty;
    private bool _hasWriteProperty;
    private readonly FilterHelper<WmiInstanceViewModel> _instanceFilterHelper;

    [ObservableProperty]
    private string _instanceFilterText = string.Empty;

    private readonly ObservableCollection<WmiInstanceViewModel> _instances = new();

    [ObservableProperty]
    private bool _isSelected;

    private bool _isUpdatingSelection = false;

    [ObservableProperty]
    private ItemStatus _itemStatus = new();

    private ObservableCollection<WmiMethod>? _methods;
    private readonly WmiNamespaceViewModel _parentNamespaceViewModel;
    private ObservableCollection<WmiProperty>? _properties;

    [ObservableProperty]
    private WmiInstanceViewModel? _selectedInstance;

    private readonly SelectionManager _selectionManager;

    [ObservableProperty]
    private ObservableCollection<WmiMethod>? _staticMethods;

    private readonly WmiClass _wmiClass;
    private readonly IWmiService _wmiService;

    public WmiClassViewModel(
              WmiClass wmiClass,
              WmiNamespaceViewModel parentNamespaceViewModel,
              IWmiService wmiService,
              IMessengerService messengerService,
              IApplicationService applicationService,
              SelectionManager selectionManager) : base(messengerService)
    {
        _wmiClass = wmiClass;
        _wmiService = wmiService;
        _applicationService = applicationService;
        _parentNamespaceViewModel = parentNamespaceViewModel ?? throw new ArgumentNullException(nameof(parentNamespaceViewModel));
        _selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));

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

        // The collection view is used for filtering and sorting instances in the UI.
        _instanceFilterHelper = new FilterHelper<WmiInstanceViewModel>(
            _instances,
            InstanceFilterPredicate
        );

        Instances = new ReadOnlyObservableCollection<WmiInstanceViewModel>(_instances);
    }

    public string ClassName => _wmiClass.ClassName;
    public string Description => _wmiClass.Description;

    /// <summary>
    /// Indicates if this class has at least one lazy property.
    /// </summary>
    public bool HasLazyProperty => _hasLazyProperty;

    /// <summary>
    /// Indicates if this class has at least one writable property.
    /// </summary>
    public bool HasWriteProperty => _hasWriteProperty;

    /// <summary>
    /// Instances of this class (read-only).
    /// </summary>
    public ReadOnlyObservableCollection<WmiInstanceViewModel> Instances { get; }

    public ICollectionView InstancesView => _instanceFilterHelper.CollectionView;
    public bool IsEventClass => WmiClass.Derivation.Contains("__Event") || WmiClass.ClassName == "__Event";
    public ManagementScope ManagementScope => _parentNamespaceViewModel.ManagementScope;

    /// <summary>
    /// Collection of all methods available for this class.
    /// </summary>
    public ObservableCollection<WmiMethod> Methods => _methods!;

    public WmiNamespaceViewModel ParentNamespaceViewModel => _parentNamespaceViewModel;

    /// <summary>
    /// Collection of all properties available for this class.
    /// </summary>
    public ObservableCollection<WmiProperty> Properties => _properties!;

    public string? Tooltip
    {
        get
        {
            switch (ItemStatus.LoadState)
            {
                case LoadState.Unknown:
                    return null;
                case LoadState.Loading:
                    return "Loading instances";
                case LoadState.Success:
                    return $"Success [{Instances?.Count ?? 0} instances]";
                case LoadState.PartialSuccess:
                    return $"Loaded Properties and Methods. Double-click to load instances.";
                case LoadState.Warning:
                    return !string.IsNullOrWhiteSpace(ItemStatus.StatusMessage) ? ItemStatus.StatusMessage : "Warning";
                case LoadState.Error:
                    return ItemStatus.Exception?.Message ?? "Failed";
                default:
                    return null;
            }
        }
    }

    public WmiClass WmiClass => _wmiClass;

    public static ObservableCollection<WmiClassViewModel> CreateFromCollection(
           IEnumerable<WmiClass> wmiClasses,
           WmiNamespaceViewModel parentNamespaceViewModel,
           IWmiService wmiService,
           IMessengerService messengerService,
           IApplicationService applicationService,
           SelectionManager selectionManager)
    {
        var viewModels = new ObservableCollection<WmiClassViewModel>();

        foreach (var wmiClass in wmiClasses)
        {
            viewModels.Add(new WmiClassViewModel(
                wmiClass,
                parentNamespaceViewModel,
                wmiService,
                messengerService,
                applicationService,
                selectionManager));
        }

        return viewModels;
    }

    /// <summary>
    /// Removes a WmiInstanceViewModel from the _instances collection and updates the view.
    /// </summary>
    public void RemoveInstance(WmiInstanceViewModel instance)
    {
        if (instance == null) return;
        lock (_collectionLock)
        {
            _instances.Remove(instance);
        }
        // Refresh the filter view if needed
        _instanceFilterHelper.CollectionView.Refresh();
        OnPropertyChanged(nameof(Instances));
        OnPropertyChanged(nameof(InstancesView));
    }

    public override string ToString() => _wmiClass.ClassName;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose and clear all collections
            ClearAndDisposeInstances();
            ClearAndDispose(_methods);
            ClearAndDispose(StaticMethods);
            ClearAndDispose(Properties);
            _wmiClass?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
            _instanceFilterHelper.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Provides immediate UI feedback when cancellation is requested and cancels the operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CancelInstanceLoadCanExecute))]
    private void CancelInstanceLoad()
    {
        try
        {
            Log.Debug("Cancellation requested for class: {ClassName}", ClassName);
            // Show immediate feedback that cancellation was requested
            PublishBusyState($"Cancellation requested for {ClassName} - operation will stop soon");

            // Immediately cancel our internal token source - this is completely non-blocking
            _cts.Cancel();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error requesting cancellation for class: {ClassName}", ClassName);
            PublishErrorState($"Error requesting cancellation for {ClassName}: {ex.Message}", ex);
        }
    }

    private bool CancelInstanceLoadCanExecute() => ItemStatus.LoadState == LoadState.Loading;

    /// <summary>
    /// Disposes all items in the collection (if IDisposable) and clears the collection.
    /// </summary>
    private static void ClearAndDispose(System.Collections.IEnumerable? collection)
    {
        if (collection == null) return;
        if (collection is System.Collections.IList list)
        {
            foreach (var item in list)
            {
                if (item is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch (Exception ex) { Log.Warning(ex, "Error disposing item in collection"); }
                }
            }
            list.Clear();
        }
        else if (collection is System.Collections.ICollection col)
        {
            var toDispose = new List<IDisposable>();
            foreach (var item in col)
            {
                if (item is IDisposable disposable)
                    toDispose.Add(disposable);
            }
            foreach (var disposable in toDispose)
            {
                try { disposable.Dispose(); }
                catch (Exception ex) { Log.Warning(ex, "Error disposing item in collection"); }
            }
            var clearMethod = col.GetType().GetMethod("Clear");
            clearMethod?.Invoke(col, null);
        }
    }

    /// <summary>
    /// Disposes all instances in the _instances collection and clears the collection.
    /// </summary>
    private void ClearAndDisposeInstances()
    {
        if (_instances.Count == 0)
            return;

        List<WmiInstanceViewModel> toDispose;
        lock (_collectionLock)
        {
            toDispose = new List<WmiInstanceViewModel>(_instances);
            _instances.Clear();
            Log.Debug("Cleared and disposed all instances for class: {ClassName}", ClassName);
        }
        foreach (var instance in toDispose)
        {
            try
            {
                instance.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing instance: {InstanceName}", instance.InstanceName);
            }
        }
    }

    /// <summary>
    /// Command to copy the class MOF to clipboard, with or without amended qualifiers.
    /// </summary>
    [RelayCommand]
    private void CopyClassMof(object? parameter)
    {
        bool useAmendedQualifiers = CommandParameterHelper.ParseBool(parameter, true);
        if (TryGetClassMof(useAmendedQualifiers, out var mof) && mof != null)
        {
            _applicationService.CopyToClipboard(mof);
            PublishSuccessState($"Class MOF copied to clipboard (amended qualifiers: {useAmendedQualifiers})");
        }
    }

    /// <summary>
    /// Command to copy the class path to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyRelativePath()
    {
        var classPath = _wmiClass.ClassPath.RelativePath;
        if (string.IsNullOrEmpty(classPath))
            return;

        _applicationService.CopyToClipboard(classPath);
        PublishSuccessState($"Copied class path: {classPath}");
    }

    [RelayCommand]
    private void CreateInstance()
    {
        try
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var managementScope = ManagementScope;
            var className = ClassName;

            // Create a new template instance
            var newInstance = WmiObjectFactory.CreateTemplateObject(className, managementScope);

            // Show the property editor dialog for the new instance
            var result = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(
                mainWindow,
                newInstance,
                _messengerService,
                $"Create Instance: {className}",
                _wmiService,
                true);

            if (result != null)
            {

                // Wrap in WmiInstance and WmiInstanceViewModel
                var wmiInstance = new WmiInstance(newInstance);
                var instanceViewModel = new WmiInstanceViewModel(
                    wmiInstance,
                    this,
                    _wmiService,
                    _messengerService,
                    _applicationService,
                    _selectionManager);

                // Add to the collection on the UI thread
                RunOnUIThread(() =>
                {
                    lock (_collectionLock)
                    {
                        _instances.Add(instanceViewModel);
                    }
                    _instanceFilterHelper.CollectionView.Refresh();
                    OnPropertyChanged(nameof(Instances));
                    OnPropertyChanged(nameof(InstancesView));
                });

                PublishSuccessState($"Instance created for class: {className}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating instance for class: {ClassName}", ClassName);
            PublishErrorState($"Error creating instance: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a WMI method from the context menu.
    /// </summary>
    /// <param name="parameter">The WmiMethod to execute.</param>
    [RelayCommand(CanExecute = nameof(ExecuteMethodCanExecute))]
    private void ExecuteMethod(object? parameter)
    {
        if (parameter is WmiMethod method)
        {
            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;

                // Use the dialog to execute the method
                if (_parentNamespaceViewModel.WmiNamespace != null)
                {
                    Presentation.Views.Dialogs.MethodExecutionDialog.ShowDialog(
                        mainWindow,
                        _wmiService,
                        _parentNamespaceViewModel.WmiNamespace,
                        _wmiClass,
                        method,
                        _messengerService);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing method execution dialog for class: {ClassName}, method: {MethodName}",
                    ClassName, method.Name);
                // Report error
                PublishErrorState($"Error showing executing method dialog: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Determines whether a method can be executed. Only static methods can be executed.
    /// </summary>
    /// <param name="parameter">The WmiMethod to check.</param>
    /// <returns>True if the method is static and can be executed, false otherwise.</returns>
    private bool ExecuteMethodCanExecute(object? parameter)
    {
        return parameter is WmiMethod method && method.IsStatic;
    }

    private bool InstanceFilterPredicate(WmiInstanceViewModel instance, string filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
               instance.InstanceName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [RelayCommand]
    private async Task LoadInstancesAsync()
    {
        if (ItemStatus.LoadState == LoadState.Loading)
            return;

        Log.Debug("Loading instances for class {ClassName}", ClassName);

        // Create a new CTS for this operation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        using var timer = OperationTimer.Start($"Loading instances for {ClassName}", _messengerService);
        try
        {
            SetStatusAndPublish(ItemStatus, LoadState.Loading, $"Loading instances for {ClassName}...");

            // Build WQL query for instances of this class
            string wqlQuery = $"SELECT * FROM {ClassName}";

            // Execute the WQL query using the service
            var wmiInstances = await _wmiService.ExecuteWmiQueryAsync(
                ParentNamespaceViewModel.ManagementScope,
                wqlQuery,
                false, // directRead: false for instance enumeration
                false, // useAmendedQualifiers: false for instances
                _cts.Token);

            // Map ManagementObject to WmiInstance and create view models for all instances at once.
            var instanceModels = wmiInstances.Select(mo => new WmiInstance(mo));
            var instanceViewModels = WmiInstanceViewModel.CreateFromCollection(
                instanceModels,
                _wmiService,
                _messengerService,
                _applicationService,
                _selectionManager,
                this);

            // Use RunOnUIThreadAsync for asynchronous UI updates to avoid hanging
            await RunOnUIThreadAsync(() =>
            {
                ClearAndDisposeInstances();
                lock (_collectionLock)
                {
                    foreach (var vm in instanceViewModels.OrderBy(vm => vm.InstanceName))
                    {
                        _instances.Add(vm);
                    }
                }
                // No need to reapply filter or refresh, FilterHelper handles it.
                OnPropertyChanged(nameof(InstanceFilterText));
                return Task.CompletedTask;
            });

            // Check if operation was cancelled and show appropriate message
            if (_cts.Token.IsCancellationRequested)
            {
                SetStatusAndPublish(ItemStatus, LoadState.Warning, $"Found {instanceViewModels.Count} instances for {ClassName} before loading was cancelled");
                Log.Warning("Found {InstanceCount} instances for {ClassName} before loading was cancelled (token signaled)", instanceViewModels.Count, ClassName);
            }
            else
            {
                SetStatusAndPublish(ItemStatus, LoadState.Success, $"Loaded {instanceViewModels.Count} instances for {ClassName}");
                UpdateInstanceFilterStatusMessage(); // Update the status bar message with "Showing" message which accounts for instance filtering.
                Log.Information("Successfully loaded {InstanceCount} instances for {ClassName}", instanceViewModels.Count, ClassName);
            }
        }
        catch (OperationCanceledException ex)
        {
            if (ex.Message.Contains("timed out"))
            {
                var msg = $"Loading instances for {ClassName} timed out - this may indicate a very large result set. Consider switching to asynchronous mode for better cancellation support. Showing {_instances.Count} partial results";
                SetStatusAndPublish(ItemStatus, LoadState.Warning, msg);
                Log.Warning(ex, "Loading instances for class {ClassName} timed out, showing {PartialCount} partial results", ClassName, _instances.Count);
            }
            else
            {
                SetStatusAndPublish(ItemStatus, LoadState.Warning, $"Loading instances for {ClassName} was cancelled - showing {_instances.Count} partial results");
                Log.Warning(ex, "Loading instances for class {ClassName} was cancelled - showing {PartialCount} partial results", ClassName, _instances.Count);
            }
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.CallCanceled || ex.ErrorCode == ManagementStatus.OperationCanceled)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Warning, $"Loading instances for {ClassName} was cancelled - showing {_instances.Count} partial results");
            Log.Warning(ex, "WMI loading instances for class {ClassName} was cancelled (ErrorCode: {ErrorCode}), showing {PartialCount} partial results",
                ClassName, ex.ErrorCode, _instances.Count);
        }
        catch (Exception ex)
        {
            SetStatusAndPublish(ItemStatus, LoadState.Error, $"Error loading instances for {ClassName}: {ex.Message}", ex);
            Log.Error(ex, "Error loading instances for class: {ClassName}", ClassName);
        }
    }

    private bool LoadInstancesCanExecute() => ItemStatus.LoadState != LoadState.Loading;

    /// <summary>
    /// Loads the methods available for this class.
    /// </summary>
    private void LoadMethods()
    {
        // Log.Debug("Loading methods for class: {ClassName}", ClassName);
        _methods = new ObservableCollection<WmiMethod>();
        StaticMethods = new ObservableCollection<WmiMethod>();

        try
        {
            // Get methods from the WmiClass
            var methods = _wmiClass.Methods;

            if (methods != null && methods.Count > 0)
            {
                foreach (var method in methods)
                {
                    // Add all methods to the methods collection
                    _methods.Add(method);

                    // Only add static methods to the static methods collection
                    if (method.IsStatic)
                    {
                        StaticMethods.Add(method);
                    }
                }
                Log.Debug("Loaded {MethodCount} methods for class: {ClassName}", methods.Count, ClassName);
            }
            else
            {
                Log.Debug("No methods found for class: {ClassName}", ClassName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading methods for class: {ClassName}", ClassName);
        }
    }

    /// <summary>
    /// Loads the properties available for this class.
    /// </summary>
    private void LoadProperties()
    {
        // Log.Debug("Loading properties for class: {ClassName}", ClassName);
        _properties = new ObservableCollection<WmiProperty>();
        try
        {
            // Get properties from the WmiClass
            var properties = _wmiClass.Properties;

            if (properties != null && properties.Count > 0)
            {
                var wmiProperties = properties
                    .Cast<System.Management.PropertyData>()
                    .Select(property => new WmiProperty(property, _wmiClass.ActualClass))
                    .ToList();
                foreach (var wmiProperty in wmiProperties)
                {
                    _properties.Add(wmiProperty);
                }
                _hasWriteProperty = wmiProperties.Any(p => !p.IsReadOnly);
                _hasLazyProperty = wmiProperties.Any(p => p.IsLazy);
                Log.Debug("Loaded {PropertyCount} properties for class: {ClassName}", properties.Count, ClassName);
            }
            else
            {
                _hasWriteProperty = false;
                _hasLazyProperty = false;
                Log.Debug("No properties found for class: {ClassName}", ClassName);
            }
        }
        catch (Exception ex)
        {
            _hasWriteProperty = false;
            _hasLazyProperty = false;
            Log.Warning(ex, "Error loading properties for class: {ClassName}", ClassName);
        }
    }

    partial void OnInstanceFilterTextChanged(string value)
    {
        if (_instanceFilterHelper.FilterText != value)
        {
            _instanceFilterHelper.SetFilterText(value, () =>
            {
                if (IsSelected)
                    UpdateInstanceFilterStatusMessage();
            });
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_isUpdatingSelection) return;

        if (value)
        {
            try
            {
                _isUpdatingSelection = true;

                if (_methods == null || _properties == null)
                {
                    // Load methods for this class
                    if (_methods == null)
                        LoadMethods();

                    // Load properties for this class
                    if (_properties == null)
                        LoadProperties();

                    if (ItemStatus.LoadState == LoadState.Unknown)
                    {
                        // Set the status to partial success
                        SetStatusAndPublish(ItemStatus, LoadState.PartialSuccess, $"Loaded {Properties.Count} properties and {Methods.Count} methods for {ClassName}. Double click to load instances.");
                    }
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    [RelayCommand]
    private void ShowClassMof(object? parameter)
    {
        bool useAmendedQualifiers = CommandParameterHelper.ParseBool(parameter, true);
        if (TryGetClassMof(useAmendedQualifiers, out var mof) && mof != null)
        {
            Log.Information("Showing MOF viewer dialog for {ClassName}", ClassName);
            var dialog = new WmiExplorer.Presentation.Views.Dialogs.MofViewerDialog(mof)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
    }

    /// <summary>
    /// Retrieves the MOF representation of the class, handling UseAmendedQualifiers and error reporting.
    /// </summary>
    /// <param name="useAmendedQualifiers">Whether to use amended qualifiers.</param>
    /// <param name="mof">The resulting MOF string, or null if failed.</param>
    /// <returns>True if successful, false otherwise.</returns>
    private bool TryGetClassMof(bool useAmendedQualifiers, out string? mof)
    {
        mof = null;
        try
        {
            var managementClass = _wmiClass.ActualClass;
            if (managementClass == null)
            {
                PublishErrorState("Class data is not loaded.");
                return false;
            }

            // Store the original value to restore after operation
            bool originalValue = managementClass.Options.UseAmendedQualifiers;
            managementClass.Options.UseAmendedQualifiers = useAmendedQualifiers;

            // Get the MOF representation of the class
            _wmiService.RefreshClassAsync(managementClass);
            mof = managementClass.GetText(System.Management.TextFormat.Mof);

            // Restore the original value
            managementClass.Options.UseAmendedQualifiers = originalValue;
            _wmiService.RefreshClassAsync(managementClass);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get class MOF for: {ClassName}", ClassName);
            PublishErrorState($"Failed to get class MOF: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Updates the status message to reflect the current instance filtering state
    /// </summary>
    private void UpdateInstanceFilterStatusMessage()
    {
        if (ItemStatus.LoadState != LoadState.Success)
            return;

        var totalCount = _instances.Count;
        var filteredCount = InstancesView.Cast<object>().Count();

        string statusMessage;
        if (string.IsNullOrWhiteSpace(InstanceFilterText))
        {
            statusMessage = $"Showing {totalCount} instances for {ClassName}";
        }
        else
        {
            statusMessage = $"Filtered {filteredCount} of {totalCount} instances for {ClassName} matching '{InstanceFilterText}'";
        }

        SetStatusAndPublish(ItemStatus, LoadState.Success, statusMessage);
    }
}