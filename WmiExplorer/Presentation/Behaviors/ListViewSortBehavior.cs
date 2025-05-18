using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ListView = System.Windows.Controls.ListView;

namespace WmiExplorer.Presentation.Behaviors
{
    public class ListViewSortBehavior
    {
        private static ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private static GridViewColumnHeader? _lastHeaderClicked = null;

        public static readonly DependencyProperty EnableSortingProperty =
            DependencyProperty.RegisterAttached("EnableSorting", typeof(bool),
                typeof(ListViewSortBehavior),
                new UIPropertyMetadata(false, OnEnableSortingChanged));

        public static readonly DependencyPropertyKey IsSortedPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly("IsSorted", typeof(bool),
                typeof(ListViewSortBehavior), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsSortedProperty = IsSortedPropertyKey.DependencyProperty;

        public static readonly DependencyPropertyKey SortDirectionPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly("SortDirection", typeof(ListSortDirection?),
                typeof(ListViewSortBehavior), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SortDirectionProperty = SortDirectionPropertyKey.DependencyProperty;

        public static readonly DependencyProperty SortPropertyProperty =
            DependencyProperty.RegisterAttached("SortProperty", typeof(string),
                typeof(ListViewSortBehavior), new UIPropertyMetadata(null));        private static void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader headerClicked || headerClicked.Column == null)
                return;

            var sortProperty = GetSortProperty(headerClicked.Column);
            if (string.IsNullOrEmpty(sortProperty))
                return;

            if (sender is not ListView listView || listView.ItemsSource == null)
                return;

            // Store if the header was already sorted before clearing indicators
            bool wasAlreadySorted = headerClicked == _lastHeaderClicked || GetIsSorted(headerClicked);
            var currentDirection = GetSortDirection(headerClicked);

            // Clear previous sort indicators
            if (_lastHeaderClicked != null)
            {
                SetIsSorted(_lastHeaderClicked, false);
                SetSortDirection(_lastHeaderClicked, null);
            }

            var direction = ListSortDirection.Ascending;
            // If header was already sorted, toggle the direction
            if (wasAlreadySorted)
            {
                // If currentDirection is not null, use it; otherwise use _lastDirection
                var directionToToggle = currentDirection ?? _lastDirection;
                direction = directionToToggle == ListSortDirection.Ascending ?
                    ListSortDirection.Descending : ListSortDirection.Ascending;
            }

            var dataView = CollectionViewSource.GetDefaultView(listView.ItemsSource);
            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortProperty, direction));

            // Set sort indicators on current header
            SetIsSorted(headerClicked, true);
            SetSortDirection(headerClicked, direction);

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }

        private static void OnEnableSortingChanged(DependencyObject obj,
            DependencyPropertyChangedEventArgs e)
        {
            if (obj is not ListView listView)
                return;

            if ((bool)e.NewValue)
            {
                listView.AddHandler(GridViewColumnHeader.ClickEvent,
                    new RoutedEventHandler(ColumnHeader_Click));
            }
            else
            {
                listView.RemoveHandler(GridViewColumnHeader.ClickEvent,
                    new RoutedEventHandler(ColumnHeader_Click));
            }
        }

        private static void SetIsSorted(DependencyObject obj, bool value)
        {
            obj.SetValue(IsSortedPropertyKey, value);
        }

        private static void SetSortDirection(DependencyObject obj, ListSortDirection? value)
        {
            obj.SetValue(SortDirectionPropertyKey, value);
        }

        public static bool GetEnableSorting(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableSortingProperty);
        }

        public static bool GetIsSorted(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsSortedProperty);
        }

        public static ListSortDirection? GetSortDirection(DependencyObject obj)
        {
            return (ListSortDirection?)obj.GetValue(SortDirectionProperty);
        }

        public static string GetSortProperty(GridViewColumn column)
        {
            return (string)column.GetValue(SortPropertyProperty);
        }

        public static void SetEnableSorting(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableSortingProperty, value);
        }

        public static void SetSortProperty(DependencyObject obj, string value)
        {
            obj.SetValue(SortPropertyProperty, value);
        }
    }
}