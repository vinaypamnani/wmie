using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ListView = System.Windows.Controls.ListView;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace WmiExplorer.Presentation.Behaviors;

public class ListViewSortBehavior
{
    public static readonly DependencyProperty DefaultSortPropertyProperty =
        DependencyProperty.RegisterAttached("DefaultSortProperty", typeof(string),
            typeof(ListViewSortBehavior), new UIPropertyMetadata(null, OnDefaultSortPropertyChanged));

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
            typeof(ListViewSortBehavior), new UIPropertyMetadata(null));

    private static ListSortDirection _lastDirection = ListSortDirection.Ascending;
    private static GridViewColumnHeader? _lastHeaderClicked = null;

    public static string GetDefaultSortProperty(DependencyObject obj)
    {
        return (string)obj.GetValue(DefaultSortPropertyProperty);
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

    public static void SetDefaultSortProperty(DependencyObject obj, string value)
    {
        obj.SetValue(DefaultSortPropertyProperty, value);
    }

    public static void SetEnableSorting(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableSortingProperty, value);
    }

    public static void SetSortProperty(DependencyObject obj, string value)
    {
        obj.SetValue(SortPropertyProperty, value);
    }

    private static void ApplyDefaultSort(ListView listView, string sortProperty)
    {
        // Sort data if ItemsSource is available, otherwise wait for it
        if (listView.ItemsSource != null)
        {
            ApplySortToItemsSource(listView, sortProperty);
        }
        else
        {
            // Monitor for when ItemsSource changes
            DependencyPropertyDescriptor
                .FromProperty(ListView.ItemsSourceProperty, typeof(ListView))
                .AddValueChanged(listView, (s, e) =>
                {
                    if (listView.ItemsSource != null)
                    {
                        ApplySortToItemsSource(listView, sortProperty);
                    }
                });
        }
    }

    private static void ApplySortToItemsSource(ListView listView, string sortProperty)
    {
        // Sort data
        var view = CollectionViewSource.GetDefaultView(listView.ItemsSource);
        if (view != null)
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortProperty, ListSortDirection.Ascending));
        }

        // Find and mark column header for visual indicator
        if (listView.View is GridView gridView)
        {
            var column = gridView.Columns.FirstOrDefault(c =>
                GetSortProperty(c) == sortProperty);

            if (column != null)
            {
                // Find header using visual tree helper after rendering completes
                listView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var header = FindGridViewColumnHeader(listView, column);
                    if (header != null)
                    {
                        // Set attached properties for sorting indicators
                        SetIsSorted(header, true);
                        SetSortDirection(header, ListSortDirection.Ascending);

                        // Update tracking variables
                        _lastHeaderClicked = header;
                        _lastDirection = ListSortDirection.Ascending;
                    }
                }), DispatcherPriority.Render);
            }
        }
    }

    private static void ColumnHeader_Click(object sender, RoutedEventArgs e)
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

    private static GridViewColumnHeader? FindGridViewColumnHeader(ListView listView, GridViewColumn column)
    {
        // First wait for the ListView template to be applied
        if (!listView.IsLoaded)
            return null;

        // Find the header row presenter first
        var presenter = FindVisualChild<GridViewHeaderRowPresenter>(listView);
        if (presenter == null)
            return null;

        // Find all headers and match to our column
        foreach (var child in presenter.GetVisualChildren())
        {
            if (child is GridViewColumnHeader header && header.Column == column)
                return header;
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // Check if the child is what we're looking for
            if (child is T result)
                return result;

            // Recursively search children
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private static void OnDefaultSortPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && e.NewValue is string sortProperty && !string.IsNullOrEmpty(sortProperty))
        {
            // Apply when the ListView is loaded
            if (listView.IsLoaded)
                ApplyDefaultSort(listView, sortProperty);
            else
                listView.Loaded += (s, args) => ApplyDefaultSort(listView, sortProperty);
        }
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
}

public static class VisualTreeExtensions
{
    public static System.Collections.Generic.IEnumerable<DependencyObject> GetVisualChildren(this DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            yield return VisualTreeHelper.GetChild(parent, i);
        }
    }
}