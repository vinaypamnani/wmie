using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WmiExplorer.Presentation.ViewModels.Helpers;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class for view models that manage a results collection, filter helper, and collection view.
/// </summary>
public abstract partial class ResultsViewModelBase<T> : MessagingViewModel
{
    protected FilterHelper<T> _filterHelper;
    protected ObservableCollection<T> _results = new();
    protected ICollectionView? _resultsView;

    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// The results collection.
    /// </summary>
    public ObservableCollection<T> Results => _results;

    /// <summary>
    /// The filtered and sorted view of the results.
    /// </summary>
    public ICollectionView ResultsView => _resultsView!;

    /// <summary>
    /// Override to provide custom filtering logic.
    /// </summary>
    protected abstract bool ResultsFilterPredicate(T instance, string filter);

    protected ResultsViewModelBase(IMessengerService messengerService) : base(messengerService)
    {
        _filterHelper = new FilterHelper<T>(_results, ResultsFilterPredicate);
        _resultsView = _filterHelper.CollectionView;
    }

    /// <summary>
    /// Updates the results collection, re-wires the filter helper and collection view, and raises property changed notifications.
    /// </summary>
    protected void SetResults(IEnumerable<T> newResults)
    {
        _results = new ObservableCollection<T>(newResults);
        _filterHelper = new FilterHelper<T>(_results, ResultsFilterPredicate);
        _resultsView = _filterHelper.CollectionView;
        _filterHelper.FilterText = FilterText;
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(ResultsView));
        _resultsView?.Refresh(); // Ensure the view is refreshed after results update
    }

    /// <summary>
    /// Handles FilterText property changes to update the filter helper.
    /// </summary>
    partial void OnFilterTextChanged(string value)
    {
        _filterHelper.FilterText = value;
    }
}