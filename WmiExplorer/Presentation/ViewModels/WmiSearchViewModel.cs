using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels;

public class WmiSearchViewModel : MessagingViewModelBase
{
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
        _resultsView = CollectionViewSource.GetDefaultView(_results);
        _resultsView.Filter = FilterResult;
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
    }

    public string FilterText
    {
        get => _filterText;
        set { if (SetProperty(ref _filterText, value)) _resultsView?.Refresh(); }
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

    private bool FilterResult(object obj)
    {
        if (obj is not WmiSearchResult result)
            return false;

        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        var filter = FilterText.ToLowerInvariant();

        return result.Name.ToLowerInvariant().Contains(filter)
            || result.Path.ToLowerInvariant().Contains(filter)
            || result.Description.ToLowerInvariant().Contains(filter)
            || (result.TypeInfo?.ToLowerInvariant().Contains(filter) == true);
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
}