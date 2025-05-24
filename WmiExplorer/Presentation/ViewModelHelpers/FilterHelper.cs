using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace WmiExplorer.Presentation.ViewModelHelpers;

/// <summary>
/// Helper for filtering a collection view with debounce and predicate logic.
/// </summary>
public class FilterHelper<T> : IDisposable
{
    private readonly ICollectionView _collectionView;
    private readonly Func<T, string, bool> _filterPredicate;
    private readonly DebounceDispatcher _debouncer = new();
    private string _filterText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterHelper{T}"/> class.
    /// </summary>
    /// <param name="collection">The collection to filter.</param>
    /// <param name="filterPredicate">Predicate to determine if an item matches the filter text.</param>
    public FilterHelper(ObservableCollection<T> collection, Func<T, string, bool> filterPredicate)
    {
        _collectionView = CollectionViewSource.GetDefaultView(collection);
        _filterPredicate = filterPredicate ?? throw new ArgumentNullException(nameof(filterPredicate));
        _collectionView.Filter = FilterPredicate;
    }

    /// <summary>
    /// Gets the filtered collection view.
    /// </summary>
    public ICollectionView CollectionView => _collectionView;

    /// <summary>
    /// Gets or sets the filter text. Setting this will refresh the view with debounce.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                _debouncer.Debounce(() =>
                {
                    if (_collectionView is CollectionView cv && cv.Dispatcher != null && !cv.Dispatcher.CheckAccess())
                    {
                        cv.Dispatcher.Invoke(() => _collectionView.Refresh());
                    }
                    else
                    {
                        _collectionView.Refresh();
                    }
                });
            }
        }
    }

    private bool FilterPredicate(object item)
    {
        if (item is T typedItem)
            return _filterPredicate(typedItem, _filterText);

        return false;
    }

    public void Dispose()
    {
        _debouncer.Dispose();
    }
}
