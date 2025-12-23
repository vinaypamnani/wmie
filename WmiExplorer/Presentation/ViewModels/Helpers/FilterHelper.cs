using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WmiExplorer.Common.Helpers;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

/// <summary>
/// Helper for filtering a collection view with debounce and predicate logic.
/// </summary>
public class FilterHelper<T> : IDisposable
{
    private readonly ICollectionView _collectionView;
    private readonly DebounceDispatcher _debouncer = new();
    private readonly Func<T, string, bool> _filterPredicate;
    private string _filterText = string.Empty;

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
        set => SetFilterText(value);
    }

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
    /// Sets the filter text with an optional completion callback.
    /// </summary>
    /// <param name="value">The filter text to set.</param>
    /// <param name="onComplete">Optional callback to invoke when filtering completes.</param>
    public void SetFilterText(string value, Action? onComplete = null)
    {
        if (_filterText != value)
        {
            _filterText = value;
            _debouncer.Debounce(() =>
            {
                if (_collectionView is CollectionView cv && cv.Dispatcher != null && !cv.Dispatcher.CheckAccess())
                {
                    // Use InvokeAsync instead of Invoke to avoid blocking the calling thread
                    cv.Dispatcher.InvokeAsync(() =>
                    {
                        _collectionView.Refresh();
                        onComplete?.Invoke();
                    });
                }
                else
                {
                    _collectionView.Refresh();
                    onComplete?.Invoke();
                }
            });
        }
        else
        {
            // If the value hasn't changed, still invoke the callback immediately
            onComplete?.Invoke();
        }
    }

    private bool FilterPredicate(object item)
    {
        if (item is T typedItem)
            return _filterPredicate(typedItem, _filterText);

        return false;
    }

    #region IDisposable

    public void Dispose()
    {
        _debouncer.Dispose();
    }

    #endregion
}