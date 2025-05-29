using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiSearchViewModel : ResultsViewModelBase<WmiSearchResult>
{
    private CancellationTokenSource? _cts;
    private bool _isSearching;
    private readonly IMessagingService _messagingService;
    private bool _recursive;
    private string _searchQuery = string.Empty;
    private WmiSearchType _searchType = WmiSearchType.Class;
    private readonly Dictionary<WmiSearchType, SearchTypeState> _searchTypeStates = new();
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiSearchResult? _selectedResult;
    private readonly IWmiService _wmiService;

    public WmiSearchViewModel(IMessagingService messagingService, IWmiService wmiService)
        : base()
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize commands
        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, CanExecuteSearch);
        ClearResultsCommand = new RelayCommand(_ => ClearCurrentTypeResults());
        CancelSearchCommand = new RelayCommand(_ => CancelSearch(), _ => IsSearching);

        // Subscribe to namespace selection changes
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

    public ICommand CancelSearchCommand { get; private set; }
    public ICommand ClearResultsCommand { get; }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public ICommand JumpToClassCommand => new RelayCommand(_ => ExecuteJumpToClass(), _ => SelectedResult != null);

    public bool Recursive
    {
        get => _recursive;
        set => SetProperty(ref _recursive, value);
    }

    public ICommand SearchCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                // Refresh the command's CanExecute state
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public WmiSearchType SearchType
    {
        get => _searchType;
        set
        {
            var oldValue = _searchType;
            if (SetProperty(ref _searchType, value))
            {
                // Store current state for the previous type
                if (!_searchTypeStates.ContainsKey(oldValue))
                    _searchTypeStates[oldValue] = new SearchTypeState();

                // Always store the results
                _searchTypeStates[oldValue].Results = new List<WmiSearchResult>(_results);

                // Only update the stored query if the current query is not empty
                // (to preserve the original query that generated the results)
                if (!string.IsNullOrWhiteSpace(_searchQuery))
                {
                    _searchTypeStates[oldValue].SearchQuery = _searchQuery;
                }
                // Clear current results and query
                _results.Clear();
                _searchQuery = string.Empty;

                // Restore state for the new type if available
                if (_searchTypeStates.TryGetValue(value, out var state) && state.Results.Count > 0)
                {
                    foreach (var result in state.Results)
                        _results.Add(result);
                    _searchQuery = state.SearchQuery;
                }
                _resultsView?.Refresh();
                OnPropertyChanged(nameof(SearchQuery));
            }
        }
    }

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

    // Clear results for the current search type only
    public void ClearCurrentTypeResults()
    {
        _results.Clear();
        if (_searchTypeStates.ContainsKey(_searchType))
        {
            _searchTypeStates[_searchType].Results.Clear();
            _searchTypeStates[_searchType].SearchQuery = string.Empty;
        }
        _searchQuery = string.Empty;
        _resultsView?.Refresh();
        OnPropertyChanged(nameof(SearchQuery));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ResultsFilterPredicate(WmiSearchResult result, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        var lower = filter.ToLowerInvariant();
        bool SafeContains(Func<string?> propertyAccessor)
        {
            try
            {
                var value = propertyAccessor();
                return value != null && value.ToLowerInvariant().Contains(lower);
            }
            catch
            {
                return false;
            }
        }
        return SafeContains(() => result.Name)
            || SafeContains(() => result.Path)
            || SafeContains(() => result.Description)
            || SafeContains(() => result.TypeInfo);
    }

    // Cancel the search operation
    private void CancelSearch()
    {
        if (!IsSearching || _cts == null)
            return;

        PublishBusyState("Cancelling search...");
        _cts.Cancel();
    }

    private bool CanExecuteSearch()
    {
        return !string.IsNullOrWhiteSpace(SearchQuery) && !_isSearching;
    }

    private void ExecuteJumpToClass()
    {
        if (SelectedResult == null)
            return;

        // Use the NamespacePath property from the search result for robust navigation
        string? namespacePath = SelectedResult.NamespacePath;
        string? className = null;
        switch (SelectedResult.SearchType)
        {
            case WmiSearchType.Class:
                className = SelectedResult.Class?.ClassName;
                break;
            case WmiSearchType.Method:
                className = SelectedResult.Method?.ClassName;
                break;
            case WmiSearchType.Property:
                className = SelectedResult.Property?.ClassName;
                break;
        }
        if (!string.IsNullOrWhiteSpace(namespacePath) && !string.IsNullOrWhiteSpace(className))
        {
            _messagingService.Publish(new JumpToClassMessage(namespacePath, className));
        }
    }

    private async Task ExecuteSearchAsync()
    {
        IsSearching = true;
        PublishBusyState("Executing search...");
        _results.Clear();

        // Only clear the stored state for the current search type
        if (!_searchTypeStates.ContainsKey(SearchType))
            _searchTypeStates[SearchType] = new SearchTypeState();
        _searchTypeStates[SearchType].Results.Clear();
        _searchTypeStates[SearchType].SearchQuery = string.Empty;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        using var timer = OperationTimer.Start($"Searching for {SearchType.ToString().ToLower()}s: {SearchQuery}", MessageService!);
        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }
            var scope = SelectedNamespace.ManagementScope;
            var searchResults = await _wmiService.ExecuteSearchAsync(scope, SearchType, SearchQuery, Recursive, _cts.Token);
            var tempResults = new List<WmiSearchResult>();
            foreach (var (match, parent) in searchResults)
            {
                // Build the search result object for each match
                var searchResult = new WmiSearchResult(SearchType, match, parent);
                tempResults.Add(searchResult);
            }
            // Use the base class method to update results and related helpers
            SetResults(tempResults);

            // Store results and query for this type after search
            _searchTypeStates[SearchType].Results = new List<WmiSearchResult>(_results);
            _searchTypeStates[SearchType].SearchQuery = SearchQuery;
            
            if (_cts.IsCancellationRequested)
            {
                PublishWarningState($"Found {_results.Count} results before search was cancelled.");
            }
            else
            {
                PublishSuccessState($"Found {_results.Count} results.");
            }
        }
        catch (OperationCanceledException)
        {
            PublishWarningState("Search cancelled.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Search failed: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
            CommandManager.InvalidateRequerySuggested(); // Refresh command state
        }
    }

    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;
        SelectedNamespace = message.NamespaceViewModel;
    }

    private class SearchTypeState
    {
        public List<WmiSearchResult> Results { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
    }
}