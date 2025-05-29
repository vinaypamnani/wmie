using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WmiExplorer.Presentation.ViewModelHelpers;

namespace WmiExplorer.Common.Base
{
    /// <summary>
    /// Base class for view models that manage a results collection, filter helper, and collection view.
    /// </summary>
    public abstract class ResultsViewModelBase<T> : MessagingViewModelBase
    {
        protected ObservableCollection<T> _results = new();
        protected FilterHelper<T> _filterHelper;
        protected ICollectionView? _resultsView;
        private string _filterText = string.Empty;

        protected ResultsViewModelBase()
        {
            _filterHelper = new FilterHelper<T>(_results, ResultsFilterPredicate);
            _resultsView = _filterHelper.CollectionView;
        }

        /// <summary>
        /// The results collection.
        /// </summary>
        public ObservableCollection<T> Results => _results;

        /// <summary>
        /// The filtered and sorted view of the results.
        /// </summary>
        public ICollectionView ResultsView => _resultsView!;

        /// <summary>
        /// The filter text for the results view.
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                    _filterHelper.FilterText = value;
            }
        }

        /// <summary>
        /// Updates the results collection, re-wires the filter helper and collection view, and raises property changed notifications.
        /// </summary>
        protected void SetResults(IEnumerable<T> newResults)
        {
            _results = new ObservableCollection<T>(newResults);
            _filterHelper = new FilterHelper<T>(_results, ResultsFilterPredicate);
            _resultsView = _filterHelper.CollectionView;
            _filterHelper.FilterText = _filterText;
            OnPropertyChanged(nameof(Results));
            OnPropertyChanged(nameof(ResultsView));
            _resultsView?.Refresh(); // Ensure the view is refreshed after results update
        }

        /// <summary>
        /// Override to provide custom filtering logic.
        /// </summary>
        protected abstract bool ResultsFilterPredicate(T instance, string filter);
    }
}
