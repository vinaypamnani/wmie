using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModelHelpers;
using WmiExplorer.Services;
using System.Threading;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiQueryViewModel : MessagingViewModelBase
{
    private readonly ICacheService _cacheService;
    private bool _directRead = false;
    private readonly FilterHelper<WmiInstance> _filterHelper;
    private string _filterText = string.Empty;
    private bool _isQuerying;
    private readonly IMessagingService _messagingService;
    private string _queryText = string.Empty;
    private ObservableCollection<WmiInstance> _results = new();
    private ICollectionView? _resultsView;
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiInstance? _selectedResult;
    private bool _useAmendedQualifiers = true;
    private readonly IWmiService _wmiService;
    private CancellationTokenSource? _cts;

    public WmiQueryViewModel(IMessagingService messagingService, IWmiService wmiService, ICacheService cacheService)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize commands
        ExecuteQueryCommand = new AsyncRelayCommand(ExecuteQueryAsync, CanExecuteQuery);
        ClearResultsCommand = new RelayCommand(_ => ClearResults());
        CancelQueryCommand = new RelayCommand(_ => CancelQuery(), _ => IsQuerying);

        // Initialize filter helper for WmiInstance
        _filterHelper = new FilterHelper<WmiInstance>(
            _results,
            QueryResultsFilterPredicate
        );
        _resultsView = _filterHelper.CollectionView;

        // Subscribe to namespace selection changes
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

    public ICacheService CacheService => _cacheService;
    public ICommand ClearResultsCommand { get; }

    public bool DirectRead
    {
        get => _directRead;
        set => SetProperty(ref _directRead, value);
    }

    public ICommand ExecuteQueryCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                _filterHelper.FilterText = value;
        }
    }

    public bool IsQuerying
    {
        get => _isQuerying;
        set => SetProperty(ref _isQuerying, value);
    }

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (SetProperty(ref _queryText, value))
            {
                // Refresh the command's CanExecute state
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<WmiInstance> Results => _results;
    public ICollectionView ResultsView => _resultsView!;

    public WmiNamespaceViewModel? SelectedNamespace
    {
        get => _selectedNamespace;
        set => SetProperty(ref _selectedNamespace, value);
    }

    public WmiInstance? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                // Optionally publish a message or handle selection change
            }
        }
    }

    public bool UseAmendedQualifiers
    {
        get => _useAmendedQualifiers;
        set => SetProperty(ref _useAmendedQualifiers, value);
    }

    public ICommand CancelQueryCommand { get; }

    private bool CanExecuteQuery()
    {
        return !string.IsNullOrWhiteSpace(QueryText) && !_isQuerying;
    }

    private void ClearResults()
    {
        _results.Clear();
        RefreshResultsView();
    }

    private void CancelQuery()
    {
        if (!IsQuerying || _cts == null)
            return;

        PublishBusyState("Cancelling query...");
        _cts.Cancel();
    }

    private async Task ExecuteQueryAsync()
    {
        IsQuerying = true;
        PublishBusyState("Executing query...");
        await Task.Yield();

        _results.Clear();

        // Create a new cancellation token source for this operation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                PublishErrorState("Query text is empty.");
                return;
            }

            var scope = SelectedNamespace.ManagementScope;
            var queryString = QueryText.Trim();
            var managementObjects = await _wmiService.ExecuteWmiQueryAsync(
                scope,
                queryString,
                _directRead,
                _useAmendedQualifiers,
                cancellationToken: token);

            foreach (var mo in managementObjects)
            {
                try
                {
                    _results.Add(new WmiInstance(mo));
                }
                catch (Exception ex)
                {
                    // Log and skip invalid objects
                    System.Diagnostics.Debug.WriteLine($"Error converting ManagementObject to WmiInstance: {ex.Message}");
                }
            }

            if (_results.Count == 0)
            {
                PublishWarningState("No results returned.");
            }
            else if (token.IsCancellationRequested)
            {
                PublishWarningState($"Query cancelled. Found {_results.Count} results before cancellation.");
            }
            else
            {
                PublishSuccessState($"Query returned {_results.Count} result(s).");
            }
        }
        catch (OperationCanceledException)
        {
            PublishWarningState($"Query cancelled. Found {_results.Count} results before cancellation.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Query failed: {ex.Message}");
        }
        finally
        {
            IsQuerying = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;
        SelectedNamespace = message.NamespaceViewModel;
    }

    private bool QueryResultsFilterPredicate(WmiInstance instance, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        var lower = filter.ToLowerInvariant();

        // Helper to safely access a property for filtering
        bool SafeContains(Func<string?> propertyAccessor)
        {
            try
            {
                var value = propertyAccessor();
                return value != null && value.ToLowerInvariant().Contains(lower);
            }
            catch
            {
                // Ignore exceptions from WMI property access
                return false;
            }
        }

        // Match on multiple properties, safely
        return SafeContains(() => instance.InstanceName)
            || SafeContains(() => instance.Path.Path)
            || SafeContains(() => instance.ClassPath?.ClassName)
            || SafeContains(() => instance.ToString());
    }

    private void RefreshResultsView()
    {
        _resultsView?.Refresh();
    }
}