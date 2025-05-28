using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModelHelpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiQueryViewModel : MessagingViewModelBase
{
    private readonly ICacheService _cacheService;
    private bool _enumerateDeep = true;
    private readonly FilterHelper<WmiSearchResult> _filterHelper;
    private string _filterText = string.Empty;
    private bool _isQuerying;
    private readonly IMessagingService _messagingService;
    private string _queryText = string.Empty;
    private ObservableCollection<WmiSearchResult> _results = new();
    private ICollectionView? _resultsView;
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiSearchResult? _selectedResult;
    private bool _useAmendedQualifiers = true;
    private readonly IWmiService _wmiService;

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

        // Initialize filter helper
        _filterHelper = new FilterHelper<WmiSearchResult>(
            _results,
            QueryResultsFilterPredicate
        );
        _resultsView = _filterHelper.CollectionView;

        // Subscribe to namespace selection changes
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

    public ICacheService CacheService => _cacheService;
    public ICommand ClearResultsCommand { get; }

    public bool EnumerateDeep
    {
        get => _enumerateDeep;
        set => SetProperty(ref _enumerateDeep, value);
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

    public ObservableCollection<WmiSearchResult> Results => _results;
    public ICollectionView ResultsView => _resultsView!;

    public WmiNamespaceViewModel? SelectedNamespace
    {
        get => _selectedNamespace;
        set => SetProperty(ref _selectedNamespace, value);
    }

    public WmiSearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                // Publish message when selection changes
                _messagingService.Publish(new SelectedSearchResultChangedMessage(value));
            }
        }
    }

    public bool UseAmendedQualifiers
    {
        get => _useAmendedQualifiers;
        set => SetProperty(ref _useAmendedQualifiers, value);
    }

    private bool CanExecuteQuery()
    {
        return !string.IsNullOrWhiteSpace(QueryText) && !_isQuerying;
    }

    private void ClearResults()
    {
        _results.Clear();
        _queryText = string.Empty;
        RefreshResultsView();
        OnPropertyChanged(nameof(QueryText));
    }

    private async Task ExecuteQueryAsync()
    {
        IsQuerying = true;
        PublishBusyState("Executing query...");
        await Task.Yield(); // Yield to allow UI updates // temoporary to avoid build warnings

        _results.Clear();

        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }

            // For now, just publish a warning message as requested
            PublishWarningState("Not implemented");
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

    private bool QueryResultsFilterPredicate(WmiSearchResult result, string filter)
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
        return SafeContains(() => result.Name)
            || SafeContains(() => result.Path)
            || SafeContains(() => result.Description)
            || SafeContains(() => result.TypeInfo);
    }

    private void RefreshResultsView()
    {
        _resultsView?.Refresh();
    }
}