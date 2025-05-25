using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModelHelpers;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiSearchViewModel : MessagingViewModelBase
{
    private CancellationTokenSource? _cts;
    private readonly FilterHelper<WmiSearchResult> _filterHelper;
    private string _filterText = string.Empty;
    private bool _isSearching;
    private readonly IMessagingService _messagingService;
    private bool _recursive;
    private ObservableCollection<WmiSearchResult> _results = new();
    private ICollectionView? _resultsView;
    private string _searchQuery = string.Empty;
    private WmiSearchType _searchType = WmiSearchType.Class;
    private readonly Dictionary<WmiSearchType, SearchTypeState> _searchTypeStates = new();
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiSearchResult? _selectedResult;
    private readonly IWmiService _wmiService;

    public WmiSearchViewModel(IMessagingService messagingService, IWmiService wmiService)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Initialize messaging
        InitializeMessaging(messagingService);        // Initialize commands
        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, CanExecuteSearch);
        ClearResultsCommand = new RelayCommand(_ => ClearCurrentTypeResults());
        CancelSearchCommand = new RelayCommand(_ => CancelSearch(), _ => IsSearching);
        _filterHelper = new FilterHelper<WmiSearchResult>(
            _results,
            SearchResultsFilterPredicate
        );
        _resultsView = _filterHelper.CollectionView;
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

    public ICommand CancelSearchCommand { get; private set; }
    public ICommand ClearResultsCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                _filterHelper.FilterText = value;
        }
    }

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

    public ObservableCollection<WmiSearchResult> Results => _results;
    public ICollectionView ResultsView => _resultsView!;
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
            {                // Store current state for the previous type
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

                RefreshResultsView();
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
        RefreshResultsView();
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

        // Create a new cancellation token source for this operation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }
            var scope = SelectedNamespace.ManagementScope;
            var searchResults = await _wmiService.ExecuteSearchAsync(scope, SearchType, SearchQuery, Recursive, _cts.Token);

            foreach (var (match, parent) in searchResults)
            {
                var searchResult = new WmiSearchResult(SearchType, match, parent);
                _results.Add(searchResult);
            }

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

    private void RefreshResultsView()
    {
        _resultsView?.Refresh();
    }

    private bool SearchResultsFilterPredicate(WmiSearchResult result, string filter)
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

    // Helper class to store search state for each type
    private class SearchTypeState
    {
        public List<WmiSearchResult> Results { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
    }
}