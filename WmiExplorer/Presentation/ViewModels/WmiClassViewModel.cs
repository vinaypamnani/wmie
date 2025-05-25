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
/// ViewModel for a WMI class, supports async loading, filtering, and selection of instances.
/// </summary>
public class WmiClassViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly object _collectionLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly FilterHelper<WmiInstanceViewModel> _instanceFilterHelper;
    private readonly ObservableCollection<WmiInstanceViewModel> _instances = new();
    private InstanceLoadState _loadState = InstanceLoadState.Unknown;
    private readonly WmiNamespaceViewModel _parentNamespaceViewModel;
    private WmiInstanceViewModel? _selectedInstance;
    private readonly WmiClass _wmiClass;
    private readonly IWmiService _wmiService;

    public WmiClassViewModel(
        WmiClass wmiClass,
        WmiNamespaceViewModel parentNamespaceViewModel,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService)
    {
        // All dependencies are required for correct operation and messaging.
        _wmiClass = wmiClass;
        _wmiService = wmiService;
        _applicationService = applicationService;
        _parentNamespaceViewModel = parentNamespaceViewModel ?? throw new ArgumentNullException(nameof(parentNamespaceViewModel));

        InitializeMessaging(messagingService);

        LoadInstancesCommand = new AsyncRelayCommand(LoadInstancesAsync);
        CopyRelativePathCommand = new RelayCommand(CopyRelativePath);

        // StrongSubscribe ensures message handlers are not garbage collected.
        StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);

        // The collection view is used for filtering and sorting instances in the UI.
        _instanceFilterHelper = new FilterHelper<WmiInstanceViewModel>(
            _instances,
            InstanceFilterPredicate
        );

        Instances = new ReadOnlyObservableCollection<WmiInstanceViewModel>(_instances);
    }

    public string ClassName => _wmiClass.ClassName;
    public ICommand CopyRelativePathCommand { get; }
    public string Description => _wmiClass.Description;

    public string InstanceFilterText
    {
        get => _instanceFilterHelper.FilterText;
        set
        {
            if (_instanceFilterHelper.FilterText != value)
            {
                _instanceFilterHelper.FilterText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Instances of this class (read-only).
    /// </summary>
    public ReadOnlyObservableCollection<WmiInstanceViewModel> Instances { get; }

    public ICollectionView InstancesView => _instanceFilterHelper.CollectionView;
    public bool IsEventClass => WmiClass.Derivation.Contains("__Event") || WmiClass.ClassName == "__Event";
    public ICommand LoadInstancesCommand { get; }

    public InstanceLoadState LoadState
    {
        get => _loadState;
        set => SetProperty(ref _loadState, value);
    }

    public ManagementScope ManagementScope => _parentNamespaceViewModel.ManagementScope;
    public WmiNamespaceViewModel ParentNamespaceViewModel => _parentNamespaceViewModel;

    public WmiInstanceViewModel? SelectedInstance
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

    public WmiClass WmiClass => _wmiClass;

    public static ObservableCollection<WmiClassViewModel> CreateFromCollection(
        IEnumerable<WmiClass> wmiClasses,
        WmiNamespaceViewModel parentNamespaceViewModel,
        IWmiService wmiService,
        IMessagingService messagingService,
        IApplicationService applicationService)
    {
        var viewModels = new ObservableCollection<WmiClassViewModel>();

        foreach (var wmiClass in wmiClasses)
        {
            viewModels.Add(new WmiClassViewModel(
                wmiClass,
                parentNamespaceViewModel,
                wmiService,
                messagingService,
                applicationService));
        }

        return viewModels;
    }

    public void ForceSelection()
    {
        PublishMessage(new SelectedClassChangedMessage(this));
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
                ParentNamespaceViewModel.ManagementScope,
                ClassName,
                _cts.Token);

            if (_cts.IsCancellationRequested)
                return;

            // Map ManagementObject to WmiInstance and create view models for all instances at once.
            var instanceModels = wmiInstances.Select(mo => new WmiInstance(mo));
            var instanceViewModels = WmiInstanceViewModel.CreateFromCollection(
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
                // No need to reapply filter or refresh, FilterHelper handles it.
                OnPropertyChanged(nameof(InstanceFilterText));
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

    private void CopyRelativePath(object? parameter)
    {
        var classPath = _wmiClass.ClassPath.RelativePath;
        _applicationService.CopyToClipboard(classPath);
        PublishSuccessState($"Copied path: {classPath}");
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

    private bool InstanceFilterPredicate(WmiInstanceViewModel instance, string filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
               instance.InstanceName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public enum InstanceLoadState
{
    Unknown,
    Loading,
    Success,
    Failed
}