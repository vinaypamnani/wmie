using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
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
    private readonly FilterHelper<WmiInstanceViewModel> _instanceFilterHelper;

    [ObservableProperty]
    private string _instanceFilterText = string.Empty;

    private readonly ObservableCollection<WmiInstanceViewModel> _instances = new();

    [ObservableProperty]
    private bool _isSelected;

    private bool _isUpdatingSelection = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelInstanceLoadCommand))]
    private InstanceLoadState _loadState = InstanceLoadState.Unknown;

    private ObservableCollection<WmiMethod>? _methods;
    private readonly WmiNamespaceViewModel _parentNamespaceViewModel;

    [ObservableProperty]
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

    public override string ToString() => _wmiClass.ClassName;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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

    private bool CancelInstanceLoadCanExecute() => LoadState == InstanceLoadState.Loading;

    /// <summary>
    /// Command to copy the class MOF to clipboard, with or without amended qualifiers.
    /// </summary>
    [RelayCommand]
    private void CopyClassMof(object? parameter)
    {
        bool useAmendedQualifiers = true; // Default

        // Try to parse the parameter as bool
        if (parameter is bool b)
            useAmendedQualifiers = b;
        else if (parameter is string s && bool.TryParse(s, out var parsed))
            useAmendedQualifiers = parsed;

        try
        {
            var managementClass = _wmiClass.ActualClass;
            if (managementClass == null)
            {
                PublishErrorState("Class data is not loaded.");
                return;
            }

            // Store the original value to restore after operation
            bool originalValue = managementClass.Options.UseAmendedQualifiers;
            managementClass.Options.UseAmendedQualifiers = useAmendedQualifiers;

            // Get the MOF representation of the class
            managementClass.Get();
            string mof = managementClass.GetText(System.Management.TextFormat.Mof);

            // Restore the original value
            managementClass.Options.UseAmendedQualifiers = originalValue;
            managementClass.Get();

            _applicationService.CopyToClipboard(mof);
            PublishSuccessState($"Class MOF copied to clipboard (amended qualifiers: {useAmendedQualifiers})");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy class MOF for: {ClassName}", ClassName);
            PublishErrorState($"Failed to copy class MOF: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void CopyRelativePath()
    {
        var classPath = _wmiClass.ClassPath.RelativePath;
        _applicationService.CopyToClipboard(classPath);
        PublishSuccessState($"Copied path: {classPath}");
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
                        method);
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

    private bool LoadInstancesCanExecute() => LoadState != InstanceLoadState.Loading;

    [RelayCommand(CanExecute = nameof(LoadInstancesCanExecute))]
    private async Task LoadInstancesAsync()
    {
        if (LoadState == InstanceLoadState.Loading)
            return;

        // Create a new CTS for this operation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        using var timer = OperationTimer.Start($"Loading instances for {ClassName}", _messengerService);
        try
        {
            LoadState = InstanceLoadState.Loading;
            PublishBusyState($"Loading instances for {ClassName}");

            // Use our own cancellation token
            var wmiInstances = await _wmiService.GetInstancesAsync(
                ParentNamespaceViewModel.ManagementScope,
                ClassName,
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

            // Use RunOnUIThread for synchronous UI updates to avoid hanging
            RunOnUIThread(() =>
            {
                lock (_collectionLock)
                {
                    _instances.Clear();
                    foreach (var vm in instanceViewModels)
                    {
                        _instances.Add(vm);
                    }
                }

                // No need to reapply filter or refresh, FilterHelper handles it.
                PublishMessage(new InstancesFilteredMessage(this));
                OnPropertyChanged(nameof(InstanceFilterText));
            });

            // Check if operation was cancelled and show appropriate message
            if (_cts.Token.IsCancellationRequested)
            {
                LoadState = InstanceLoadState.Warning;
                PublishWarningState($"Found {instanceViewModels.Count} instances for {ClassName} before loading was cancelled");
            }
            else
            {
                LoadState = InstanceLoadState.Success;
                PublishSuccessState($"Loaded {instanceViewModels.Count} instances for {ClassName}");
            }
        }
        catch (OperationCanceledException ex)
        {
            LoadState = InstanceLoadState.Warning;
            if (ex.Message.Contains("timed out"))
            {
                Log.Warning(ex, "Loading instances for class {ClassName} timed out, showing {PartialCount} partial results",
                    ClassName, _instances.Count);
                // Synchronous operation timed out
                PublishWarningState($"Loading instances for {ClassName} timed out - this may indicate a very large result set. Consider switching to asynchronous mode for better cancellation support. Showing {_instances.Count} partial results");
            }
            else
            {
                Log.Warning(ex, "Loading instances for class {ClassName} was cancelled, showing {PartialCount} partial results",
                    ClassName, _instances.Count);
                // Regular cancellation
                PublishWarningState($"Loading instances for {ClassName} was cancelled - showing {_instances.Count} partial results");
            }
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.CallCanceled || ex.ErrorCode == ManagementStatus.OperationCanceled)
        {
            Log.Warning(ex, "WMI loading instances for class {ClassName} was cancelled (ErrorCode: {ErrorCode}), showing {PartialCount} partial results",
                ClassName, ex.ErrorCode, _instances.Count);
            // Handle WMI cancellation errors - show partial results that we already loaded
            LoadState = InstanceLoadState.Warning;
            PublishWarningState($"Loading instances for {ClassName} was cancelled - showing {_instances.Count} partial results");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading instances for class: {ClassName}", ClassName);
            LoadState = InstanceLoadState.Failed;
            PublishErrorState($"Error loading instances for {ClassName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads the methods available for this class.
    /// </summary>
    private void LoadMethods()
    {
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
        Properties = new ObservableCollection<WmiProperty>();

        try
        {
            // Get properties from the WmiClass
            var properties = _wmiClass.Properties;

            if (properties != null && properties.Count > 0)
            {
                foreach (var property in properties)
                {
                    // Add all properties to the properties collection
                    Properties.Add(new WmiProperty(property, _wmiClass.ActualClass));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading properties for class: {ClassName}", ClassName);
        }
    }

    partial void OnInstanceFilterTextChanged(string value)
    {
        if (_instanceFilterHelper.FilterText != value)
        {
            _instanceFilterHelper.FilterText = value;
            if (IsSelected)
                PublishMessage(new InstancesFilteredMessage(this));
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

                // Load methods for this class
                LoadMethods();

                // Load properties for this class
                LoadProperties();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }
}

public enum InstanceLoadState
{
    Unknown,
    Loading,
    Warning,
    Success,
    Failed
}