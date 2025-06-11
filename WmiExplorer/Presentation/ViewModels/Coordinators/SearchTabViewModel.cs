using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

public partial class SearchTabViewModel : ResultsViewModelBase<WmiSearchResult>
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelSearchCommand))]
    private bool _isSearching;

    [ObservableProperty]
    private bool _recursive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSearchCommand))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private WmiSearchType _searchType = WmiSearchType.Class;

    private readonly Dictionary<WmiSearchType, SearchTypeState> _searchTypeStates = new();

    [ObservableProperty]
    private WmiNamespaceViewModel? _selectedNamespace;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JumpToClassCommand))]
    private WmiSearchResult? _selectedResult;

    private readonly ISelectionService _selectionService;
    private readonly IWmiService _wmiService;

    public SearchTabViewModel(IMessengerService messengerService, IWmiService wmiService, ISelectionService selectionService)
                    : base(messengerService)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

        // Subscribe to unified selection changes
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);
    }

    // Clear results for the current search type only
    public void ClearCurrentTypeResults()
    {
        _results.Clear();
        if (_searchTypeStates.ContainsKey(SearchType))
        {
            _searchTypeStates[SearchType].Results.Clear();
            _searchTypeStates[SearchType].SearchQuery = string.Empty;
        }
        SearchQuery = string.Empty;
        _resultsView?.Refresh();
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
            || SafeContains(() => result.Description) || SafeContains(() => result.TypeInfo);
    }

    [RelayCommand(CanExecute = nameof(CancelSearchCanExecute))]
    private void CancelSearch()
    {
        if (!IsSearching || _cts == null)
            return;

        PublishBusyState("Cancelling search...");
        _cts.Cancel();
    }

    private bool CancelSearchCanExecute() => IsSearching;

    [RelayCommand]
    private void ClearResults()
    {
        ClearCurrentTypeResults();
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
            PublishMessage(new JumpToClassMessage(namespacePath, className));
        }
    }

    [RelayCommand(CanExecute = nameof(ExecuteSearchCanExecute))]
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
        using var timer = OperationTimer.Start($"Searching for {SearchType.ToString().ToLower()}s: {SearchQuery}", _messengerService);
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

    private bool ExecuteSearchCanExecute() => !string.IsNullOrWhiteSpace(SearchQuery) && !IsSearching;

    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        var selectedObject = message.SelectionService.SelectedObject;

        // Only respond to namespace selections
        if (selectedObject is WmiNamespaceViewModel namespaceVm && namespaceVm != SelectedNamespace)
        {
            SelectedNamespace = namespaceVm;
        }
    }

    [RelayCommand(CanExecute = nameof(JumpToClassCanExecute))]
    private void JumpToClass()
    {
        ExecuteJumpToClass();
    }

    private bool JumpToClassCanExecute() => SelectedResult != null;

    /// <summary>
    /// Called when SearchType property changes
    /// </summary>
    partial void OnSearchTypeChanged(WmiSearchType oldValue, WmiSearchType newValue)
    {
        // Store current state for the previous type
        if (!_searchTypeStates.ContainsKey(oldValue))
            _searchTypeStates[oldValue] = new SearchTypeState();

        // Always store the results
        _searchTypeStates[oldValue].Results = new List<WmiSearchResult>(_results);

        // Only update the stored query if the current query is not empty
        // (to preserve the original query that generated the results)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            _searchTypeStates[oldValue].SearchQuery = SearchQuery;
        }

        // Clear current results and query
        _results.Clear();
        _selectionService.ClearSelections();

        SearchQuery = string.Empty;

        // Restore state for the new type if available
        if (_searchTypeStates.TryGetValue(newValue, out var state) && state.Results.Count > 0)
        {
            foreach (var result in state.Results)
                _results.Add(result);
            SearchQuery = state.SearchQuery;
        }
        _resultsView?.Refresh();
    }

    /// <summary>
    /// Called when SelectedResult property changes
    /// </summary>
    partial void OnSelectedResultChanged(WmiSearchResult? value)
    {
        // Update SelectionService with the new selection
        _selectionService.SetSelectedObject(value);
    }

    private class SearchTypeState
    {
        public List<WmiSearchResult> Results { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
    }
}