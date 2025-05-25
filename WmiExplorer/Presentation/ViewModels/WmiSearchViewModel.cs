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
    private readonly FilterHelper<WmiSearchResult> _filterHelper;
    private string _filterText = string.Empty;
    private bool _isSearching;
    private readonly IMessagingService _messagingService;
    private bool _recursive;
    private ObservableCollection<WmiSearchResult> _results = new();
    private ICollectionView? _resultsView;
    private string _searchQuery = string.Empty;
    private WmiSearchType _searchType = WmiSearchType.Class;
    private WmiNamespaceViewModel? _selectedNamespace;
    private WmiSearchResult? _selectedResult;
    private readonly IWmiService _wmiService;

    public WmiSearchViewModel(IMessagingService messagingService, IWmiService wmiService)
    {
        _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize commands
        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, CanExecuteSearch);
        _filterHelper = new FilterHelper<WmiSearchResult>(
            _results,
            SearchResultsFilterPredicate
        );
        _resultsView = _filterHelper.CollectionView;
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

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
        set { if (SetProperty(ref _searchType, value)) RefreshResultsView(); }
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

    private bool CanExecuteSearch()
    {
        return !string.IsNullOrWhiteSpace(SearchQuery) && !_isSearching;
    }

    private async Task ExecuteSearchAsync()
    {
        IsSearching = true;
        PublishBusyState("Executing search...");
        _results.Clear();

        try
        {
            if (SelectedNamespace == null)
            {
                PublishErrorState("No namespace selected.");
                return;
            }
            var scope = SelectedNamespace.ManagementScope;
            var searchResults = await _wmiService.ExecuteSearchAsync(scope, SearchType, SearchQuery, Recursive);

            foreach (var (match, parent) in searchResults)
            {
                var searchResult = new WmiSearchResult(SearchType, match, parent);
                _results.Add(searchResult);
            }

            PublishSuccessState($"Found {_results.Count} results.");
        }
        catch (Exception ex)
        {
            PublishErrorState($"Search failed: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
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
}