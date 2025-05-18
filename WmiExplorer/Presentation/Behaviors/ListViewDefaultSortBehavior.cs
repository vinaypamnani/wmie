using System;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace WmiExplorer.Presentation.Behaviors
{
    public static class ListViewDefaultSortBehavior
    {
        public static readonly DependencyProperty DefaultSortPropertyProperty =
            DependencyProperty.RegisterAttached(
                "DefaultSortProperty",
                typeof(string),
                typeof(ListViewDefaultSortBehavior),
                new PropertyMetadata(null, OnDefaultSortPropertyChanged));

        public static string GetDefaultSortProperty(DependencyObject obj) =>
            (string)obj.GetValue(DefaultSortPropertyProperty);

        public static void SetDefaultSortProperty(DependencyObject obj, string value) =>
            obj.SetValue(DefaultSortPropertyProperty, value);

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
                    .AddValueChanged(listView, (s, e) => {
                        if (listView.ItemsSource != null)
                        {
                            ApplySortToItemsSource(listView, sortProperty);
                        }
                    });
            }
        }        private static void ApplySortToItemsSource(ListView listView, string sortProperty)
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
                    ListViewSortBehavior.GetSortProperty(c) == sortProperty);

                if (column != null)
                {
                    // Find header using visual tree helper after rendering completes
                    listView.Dispatcher.BeginInvoke(new Action(() => {
                        var header = FindGridViewColumnHeader(listView, column);
                        if (header != null)
                        {
                            // Set attached properties for sorting indicators
                            SetIsSorted(header, true);
                            SetSortDirection(header, ListSortDirection.Ascending);

                            // Update the LastHeaderClicked field in ListViewSortBehavior via reflection
                            UpdateLastHeaderClicked(header);
                        }
                    }), DispatcherPriority.Render);
                }
            }
        }

        // Use reflection to update _lastHeaderClicked field in ListViewSortBehavior
        private static void UpdateLastHeaderClicked(GridViewColumnHeader header)
        {
            try
            {
                var field = typeof(ListViewSortBehavior).GetField("_lastHeaderClicked",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);

                if (field != null)
                {
                    field.SetValue(null, header);

                    // Also set _lastDirection to Ascending
                    var dirField = typeof(ListViewSortBehavior).GetField("_lastDirection",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.NonPublic);

                    if (dirField != null)
                    {
                        dirField.SetValue(null, ListSortDirection.Ascending);
                    }
                }
            }
            catch
            {
                // Silently fail if reflection doesn't work
            }
        }
          // Helper methods to set the attached properties directly
        private static void SetIsSorted(DependencyObject obj, bool value)
        {
            // We need to use the PropertyKey to set read-only properties
            obj.SetValue(ListViewSortBehavior.IsSortedPropertyKey, value);
        }

        private static void SetSortDirection(DependencyObject obj, ListSortDirection? value)
        {
            // We need to use the PropertyKey to set read-only properties
            obj.SetValue(ListViewSortBehavior.SortDirectionPropertyKey, value);
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
}
