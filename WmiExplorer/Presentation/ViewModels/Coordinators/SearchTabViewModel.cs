using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Models;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

public partial class SearchTabViewModel : ResultsViewModelBase<WmiSearchResult>
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _excludeLDAP = true;

    [ObservableProperty]
    private bool _excludeLdapEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelSearchCommand))]
    private bool _isSearching;

    // Dictionary to store state per namespace
    private readonly Dictionary<string, SearchNamespaceState> _namespaceStates = new();

    [ObservableProperty]
    private bool _recursive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSearchCommand))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private WmiSearchType _searchType = WmiSearchType.Class;

    private readonly Dictionary<WmiSearchType, SearchTypeState> _searchTypeStates = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JumpToClassCommand))]
    private WmiSearchResult? _selectedResult;

    [ObservableProperty]
    private TabStatus _tabStatus;

    private readonly IWmiService _wmiService;

    public SearchTabViewModel(
           IMessengerService messengerService,
           IWmiService wmiService,
           SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Initialize tab status with messenger service
        _tabStatus = new TabStatus(messengerService, AppState.Ready, "Enter a search term and click Search.", "Search for WMI objects");

        // Initialize LDAP enabled state
        UpdateExcludeLdapEnabled();
    }

    /// <summary>
    /// Gets the header text for the Search tab with count
    /// </summary>
    public string TabHeader
    {
        get
        {
            var filteredCount = Results.Count;
            if (filteredCount > 0)
            {
                return $"Search [{filteredCount}]";
            }
            return "Search";
        }
    }

    /// <summary>
    /// Clears all namespace states (useful for cleanup)
    /// </summary>
    public void ClearAllNamespaceStates()
    {
        foreach (var state in _namespaceStates.Values)
        {
            foreach (var searchState in state.SearchTypeStates.Values)
            {
                DisposeResults(searchState.Results);
            }
        }
        _namespaceStates.Clear();
    }

    // Clear results for the current search type only
    public void ClearCurrentTypeResults()
    {
        _results.Clear();
        if (_searchTypeStates.ContainsKey(SearchType))
        {
            DisposeResults(_searchTypeStates[SearchType].Results);
            _searchTypeStates[SearchType].Results.Clear();
            // _searchTypeStates[SearchType].SearchQuery = string.Empty;
        }
        // SearchQuery = string.Empty;
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

    /// <summary>
    /// Called after results are updated. Re-subscribes to the new collection view and updates tab header.
    /// </summary>
    protected override void OnResultsUpdated()
    {
        // Immediately update tab header since results just changed
        OnPropertyChanged(nameof(TabHeader));
    }

    /// <summary>
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// Restores state for the new namespace.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        // Restore state for the new namespace
        RestoreNamespaceState(selectedNamespace);

        // Update LDAP enabled state for the new namespace
        UpdateExcludeLdapEnabled();

        // Update tab header
        OnPropertyChanged(nameof(TabHeader));
    }

    /// <summary>
    /// Called before the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// Saves state for the current namespace before it changes.
    /// </summary>
    protected override void OnSelectedNamespaceChanging(WmiNamespaceViewModel? currentNamespace)
    {
        // Save state for the current namespace before it changes
        SaveCurrentNamespaceState(currentNamespace);
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

    private void ClearCurrentState()
    {
        SearchQuery = string.Empty;
        SearchType = WmiSearchType.Class;
        Recursive = false;
        ExcludeLDAP = true;
        _results.Clear();
        _searchTypeStates.Clear();
    }

    [RelayCommand]
    private void ClearResults()
    {
        ClearCurrentTypeResults();
        SelectionManager.PropertyGrid.ClearPropertyGrid();
    }

    private void DisposeResults(IEnumerable<WmiSearchResult> results)
    {
        foreach (var result in results)
        {
            result.Class?.Dispose();
            // If in the future Method or Property become disposable, dispose them here as well.
        }
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
        TabStatus.SetBusy($"Executing search for '{SearchQuery}' [{SearchType}]...");
        _results.Clear();

        // Only clear the stored state for the current search type
        if (!_searchTypeStates.ContainsKey(SearchType))
            _searchTypeStates[SearchType] = new SearchTypeState();

        // Dispose previous results before clearing
        DisposeResults(_searchTypeStates[SearchType].Results);
        _searchTypeStates[SearchType].Results.Clear();
        _searchTypeStates[SearchType].SearchQuery = string.Empty;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        using var timer = OperationTimer.Start($"Searching for {SearchType.ToString().ToLower()}s: {SearchQuery}", _messengerService);

        try
        {
            if (SelectionManager.SelectedNamespace == null)
            {
                TabStatus.SetError("No namespace selected.");
                return;
            }
            var scope = SelectionManager.SelectedNamespace.ManagementScope;
            var failureCount = 0;
            var searchResults = await _wmiService.ExecuteSearchAsync(
                scope,
                SearchType,
                SearchQuery,
                Recursive,
                ExcludeLDAP,
                _cts.Token,
                (progressMessage, currentFailureCount) =>
                {
                    failureCount = currentFailureCount; // Track the latest failure count
                    if (failureCount > 0)
                        PublishBusyState($"{progressMessage} ({failureCount} namespace access failures)");
                    else
                        PublishBusyState(progressMessage);
                });
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
            _searchTypeStates[SearchType].SearchQuery = SearchQuery; if (_cts.IsCancellationRequested)
            {
                if (failureCount > 0)
                {
                    TabStatus.SetWarning($"Found {_results.Count} results before search was cancelled. {failureCount} namespace access failures occurred. Check the Log tab for details.");
                }
                else
                {
                    TabStatus.SetWarning($"Found {_results.Count} results before search was cancelled.");
                }
            }
            else
            {
                if (failureCount > 0)
                {
                    TabStatus.SetWarning($"Found {_results.Count} results. {failureCount} namespace access failures occurred. Check the Log tab for details.");
                }
                else
                {
                    TabStatus.SetSuccess($"Found {_results.Count} results for '{SearchQuery}' [{SearchType}].");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("WMI search cancelled: SearchType={SearchType}, SearchQuery={SearchQuery}", SearchType, SearchQuery);
            TabStatus.SetWarning("Search cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WMI search execution failed: SearchType={SearchType}, SearchQuery={SearchQuery}", SearchType, SearchQuery);
            TabStatus.SetError($"Search failed: {ex.Message}", ex);
        }
        finally
        {
            IsSearching = false;
            CommandManager.InvalidateRequerySuggested(); // Refresh command state
        }
    }

    private bool ExecuteSearchCanExecute() => !string.IsNullOrWhiteSpace(SearchQuery) && !IsSearching;

    [RelayCommand(CanExecute = nameof(JumpToClassCanExecute))]
    private void JumpToClass()
    {
        ExecuteJumpToClass();
    }

    private bool JumpToClassCanExecute() => SelectedResult != null;

    // Update LDAP enabled state when Recursive changes
    partial void OnRecursiveChanged(bool value)
    {
        UpdateExcludeLdapEnabled();
    }

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

        // Clear current type results
        _results.Clear();
        SelectionManager.PropertyGrid.ClearPropertyGrid();


        // Restore state for the new type if available
        if (_searchTypeStates.TryGetValue(newValue, out var state) && state.Results.Count > 0)
        {
            foreach (var result in state.Results)
                _results.Add(result);
            SearchQuery = state.SearchQuery;

            // Update the current search query to match the stored one
            PublishReadyState($"Restored {state.Results.Count} results for '{state.SearchQuery}' [{newValue}].");
        }
        else
        {
            // Update status bar to indicate the active search type
            PublishReadyState($"Search type changed to: {newValue}");
        }
        _resultsView?.Refresh();
    }

    partial void OnSelectedResultChanged(WmiSearchResult? value)
    {
        // Update status bar
        PublishReadyState($"Showing details for search result: {value?.Name} [{value?.SearchType}]");
    }

    private void RestoreNamespaceState(WmiNamespaceViewModel? selectedNamespace)
    {
        if (selectedNamespace?.NamespacePath == null)
        {
            // Clear everything if no namespace selected
            ClearCurrentState();
            return;
        }

        if (_namespaceStates.TryGetValue(selectedNamespace.NamespacePath, out var state))
        {
            // Restore state for this namespace
            SearchQuery = state.SearchQuery;
            SearchType = state.SearchType;
            Recursive = state.Recursive;
            ExcludeLDAP = state.ExcludeLDAP;

            // Restore search type states
            _searchTypeStates.Clear();
            foreach (var kvp in state.SearchTypeStates)
            {
                _searchTypeStates[kvp.Key] = kvp.Value;
            }

            // Restore current results
            _results.Clear();
            if (_searchTypeStates.TryGetValue(SearchType, out var currentState))
            {
                foreach (var result in currentState.Results)
                {
                    _results.Add(result);
                }
            }
        }
        else
        {
            // New namespace - clear everything
            ClearCurrentState();
        }
    }

    private void SaveCurrentNamespaceState(WmiNamespaceViewModel? namespaceViewModel = null)
    {
        // Use provided namespace or fall back to previous namespace from SelectionManager
        var namespaceToSave = namespaceViewModel ?? SelectionManager.PreviousNamespace;
        if (namespaceToSave?.NamespacePath == null) return;

        var state = new SearchNamespaceState
        {
            SearchQuery = SearchQuery,
            SearchType = SearchType,
            Recursive = Recursive,
            ExcludeLDAP = ExcludeLDAP,
            SearchTypeStates = new Dictionary<WmiSearchType, SearchTypeState>(_searchTypeStates)
        };

        _namespaceStates[namespaceToSave.NamespacePath] = state;
    }

    private void UpdateExcludeLdapEnabled()
    {
        var selectedNamespace = SelectionManager.SelectedNamespace;
        if (selectedNamespace == null)
        {
            ExcludeLdapEnabled = false;
            return;
        }

        var namespacePath = selectedNamespace.WmiNamespace?.RelativePath?.ToLowerInvariant();

        // Show the checkbox only when LDAP namespace would actually be in scope:
        // 1. We're at root level AND Recursive is enabled (would search root\directory\ldap)
        // 2. We're at root\directory level AND Recursive is enabled (would search ldap)
        // The LDAP namespace is only included when recursive search would reach it

        bool isAtRootWithRecursive = (string.IsNullOrEmpty(namespacePath) || namespacePath == "root") && Recursive;
        bool isAtDirectoryWithRecursive = namespacePath == "root\\directory" && Recursive;

        ExcludeLdapEnabled = isAtRootWithRecursive || isAtDirectoryWithRecursive;
    }

    private class SearchNamespaceState
    {
        public bool ExcludeLDAP { get; set; } = true;
        public bool Recursive { get; set; } = false;
        public string SearchQuery { get; set; } = string.Empty;
        public WmiSearchType SearchType { get; set; } = WmiSearchType.Class;
        public Dictionary<WmiSearchType, SearchTypeState> SearchTypeStates { get; set; } = new();
    }

    private class SearchTypeState
    {
        public List<WmiSearchResult> Results { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
    }
}