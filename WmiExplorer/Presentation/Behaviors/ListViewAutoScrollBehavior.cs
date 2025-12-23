using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using System.Windows.Media;
using System.Windows.Threading;

namespace WmiExplorer.Presentation.Behaviors;

public static class ListViewAutoScrollBehavior
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(ListViewAutoScrollBehavior),
            new UIPropertyMetadata(false, OnAutoScrollChanged));

    public static readonly DependencyProperty SelectedItemMonitorProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItemMonitor",
            typeof(object),
            typeof(ListViewAutoScrollBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemMonitorChanged));

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);

    public static object GetSelectedItemMonitor(DependencyObject obj) => obj.GetValue(SelectedItemMonitorProperty);

    /// <summary>
    /// Scrolls the ListView to make the selected item visible using virtualization-friendly methods
    /// </summary>
    public static void ScrollToSelectedItem(ListView listView)
    {
        var selectedItem = GetSelectedItemMonitor(listView) ?? listView.SelectedItem;
        if (listView == null || selectedItem == null)
            return;

        // Method 1: Try using ScrollIntoView (works with virtualization)
        if (TryScrollIntoView(listView, selectedItem))
            return;

        // Method 2: Try index-based scrolling
        if (TryScrollByIndex(listView, selectedItem))
            return;

        // Method 3: Try container generation with limited attempts
        TryScrollWithContainerGeneration(listView, selectedItem, 3);
    }

    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    public static void SetSelectedItemMonitor(DependencyObject obj, object value) => obj.SetValue(SelectedItemMonitorProperty, value);

    private static double EstimateItemHeight(ListView listView)
    {
        try
        {
            // Try to get height from first visible container
            if (listView.Items.Count > 0)
            {
                var firstContainer = listView.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                if (firstContainer != null)
                {
                    return firstContainer.ActualHeight;
                }
            }

            // Fallback: estimate based on font size
            return listView.FontSize * 1.5 + 4; // Rough estimate
        }
        catch
        {
            return 20; // Default fallback
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject visual)
    {
        if (visual is ScrollViewer scrollViewer)
            return scrollViewer;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
        {
            var child = VisualTreeHelper.GetChild(visual, i);
            var result = FindScrollViewer(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && (bool)e.NewValue)
        {
            // Handle selection changes
            listView.SelectionChanged += (s, args) =>
            {
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => ScrollToSelectedItem(listView)));
            };

            // Handle data context changes
            listView.DataContextChanged += (s, args) =>
            {
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(() => ScrollToSelectedItem(listView)));
            };

            // Handle items source changes
            DependencyPropertyDescriptor
                .FromProperty(ListView.ItemsSourceProperty, typeof(ListView))
                .AddValueChanged(listView, (s, args) =>
                {
                    listView.Dispatcher.BeginInvoke(
                        DispatcherPriority.ContextIdle,
                        new Action(() => ScrollToSelectedItem(listView)));
                });

            // Handle loaded event
            listView.Loaded += (s, args) =>
            {
                // Handle collection changes if items source implements INotifyCollectionChanged
                if (listView.ItemsSource is INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged += (sender, collectionArgs) =>
                    {
                        listView.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new Action(() => ScrollToSelectedItem(listView)));
                    };
                }

                // Initial scroll
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() => ScrollToSelectedItem(listView)));
            };
        }
    }

    private static void OnSelectedItemMonitorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView)
        {
            // When selection changes, scroll to the new item
            if (e.NewValue != null && !Equals(e.OldValue, e.NewValue))
            {
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => ScrollToSelectedItem(listView)));
            }
        }
    }

    private static bool TryScrollByIndex(ListView listView, object selectedItem)
    {
        try
        {
            // Get the index of the selected item
            var selectedIndex = listView.Items.IndexOf(selectedItem);
            if (selectedIndex < 0)
                return false;

            // Find the ScrollViewer
            var scrollViewer = FindScrollViewer(listView);
            if (scrollViewer == null)
                return false;

            // Calculate approximate scroll position based on item height
            var itemHeight = EstimateItemHeight(listView);
            if (itemHeight > 0)
            {
                var targetOffset = selectedIndex * itemHeight;

                // Ensure we don't scroll beyond the content
                var maxOffset = Math.Max(0, scrollViewer.ScrollableHeight);
                targetOffset = Math.Min(targetOffset, maxOffset);

                scrollViewer.ScrollToVerticalOffset(targetOffset);
                return true;
            }

            // Fallback: scroll by item count
            if (listView.Items.Count > 0)
            {
                var ratio = (double)selectedIndex / listView.Items.Count;
                var targetOffset = ratio * scrollViewer.ScrollableHeight;
                scrollViewer.ScrollToVerticalOffset(targetOffset);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryScrollIntoView(ListView listView, object selectedItem)
    {
        try
        {
            // This is the most virtualization-friendly approach
            listView.ScrollIntoView(selectedItem);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryScrollWithContainerGeneration(ListView listView, object selectedItem, int attemptsLeft)
    {
        if (attemptsLeft <= 0)
            return;

        // Try to get the container
        var container = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
        if (container != null)
        {
            container.BringIntoView();
            return;
        }

        // If container doesn't exist, force a single layout update and try again
        listView.UpdateLayout();

        container = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
        if (container != null)
        {
            container.BringIntoView();
            return;
        }

        // Schedule next attempt with minimal delay
        listView.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => TryScrollWithContainerGeneration(listView, selectedItem, attemptsLeft - 1)));
    }
}