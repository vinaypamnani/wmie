using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
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

    public static readonly DependencyProperty DisableVirtualizationProperty =
        DependencyProperty.RegisterAttached(
            "DisableVirtualization",
            typeof(bool),
            typeof(ListViewAutoScrollBehavior),
            new UIPropertyMetadata(false, OnDisableVirtualizationChanged));

    public static readonly DependencyProperty SelectedItemMonitorProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItemMonitor",
            typeof(object),
            typeof(ListViewAutoScrollBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemMonitorChanged));

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);

    public static bool GetDisableVirtualization(DependencyObject obj) => (bool)obj.GetValue(DisableVirtualizationProperty);

    public static object GetSelectedItemMonitor(DependencyObject obj) => obj.GetValue(SelectedItemMonitorProperty);

    /// <summary>
    /// Scrolls the ListView to make the selected item visible
    /// </summary>
    public static void ScrollToSelectedItem(ListView listView)
    {
        if (listView == null || listView.SelectedItem == null)
            return;

        // Try to scroll immediately if possible
        var container = listView.ItemContainerGenerator.ContainerFromItem(listView.SelectedItem) as ListViewItem;
        if (container != null)
        {
            // Container exists, bring it into view (without focus)
            container.BringIntoView();

            // Use UpdateLayout to ensure UI is fully updated
            listView.UpdateLayout();
        }
        else
        {
            // Container doesn't exist yet, try with multiple attempts
            TryScrollToSelectedItem(listView, 5);
        }
    }

    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    public static void SetDisableVirtualization(DependencyObject obj, bool value) => obj.SetValue(DisableVirtualizationProperty, value);

    public static void SetSelectedItemMonitor(DependencyObject obj, object value) => obj.SetValue(SelectedItemMonitorProperty, value);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && (bool)e.NewValue)
        {
            // 1. Handle ListView.SelectionChanged (when user clicks an item)
            listView.SelectionChanged += (s, args) => ScrollToSelectedItem(listView);

            // 2. Handle DataContextChanged
            listView.DataContextChanged += (s, args) =>
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(() => ScrollToSelectedItem(listView)));

            // 3. Handle ItemsSource changes
            DependencyPropertyDescriptor
                .FromProperty(ListView.ItemsSourceProperty, typeof(ListView))
                .AddValueChanged(listView, (s, args) =>
                    listView.Dispatcher.BeginInvoke(
                        DispatcherPriority.ContextIdle,
                        new Action(() => ScrollToSelectedItem(listView))));

            // 4. Handle items collection changes
            listView.Loaded += (s, args) =>
            {
                if (listView.ItemsSource is INotifyCollectionChanged notifyCollection)
                {
                    // When items are added/removed/reset
                    notifyCollection.CollectionChanged += (sender, collectionArgs) =>
                    {
                        // Wait for containers to be generated
                        listView.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new Action(() => ScrollToSelectedItem(listView)));
                    };
                }
                else if (listView.ItemsSource is ICollectionView collectionView)
                {
                    // Handle collection view changes
                    collectionView.CollectionChanged += (sender, collectionArgs) =>
                    {
                        listView.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new Action(() => ScrollToSelectedItem(listView)));
                    };
                }

                // Initial scroll if necessary
                listView.Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() => ScrollToSelectedItem(listView)));
            };

            // 5. Track ItemContainerGenerator status changes for virtualized lists
            listView.ItemContainerGenerator.StatusChanged += (s, args) =>
            {
                if (listView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    listView.Dispatcher.BeginInvoke(
                        DispatcherPriority.Loaded,
                        new Action(() => ScrollToSelectedItem(listView)));
                }
            };
        }
    }

    private static void OnDisableVirtualizationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && (bool)e.NewValue)
        {
            // Disable UI virtualization for this ListView
            VirtualizingPanel.SetIsVirtualizing(listView, false);
            ScrollViewer.SetCanContentScroll(listView, false);
        }
    }

    private static void OnSelectedItemMonitorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView)
        {
            // When null -> non-null: scroll to the new selected item
            if (e.OldValue == null && e.NewValue != null)
            {
                ScheduleMultipleScrollAttempts(listView);
            }
            // When changing from one selection to another
            else if (e.OldValue != null && e.NewValue != null && !e.OldValue.Equals(e.NewValue))
            {
                ScheduleMultipleScrollAttempts(listView);
            }
        }
    }

    private static void ScheduleMultipleScrollAttempts(ListView listView)
    {
        // Make multiple scroll attempts with increasing delays
        for (int i = 0; i < 5; i++)
        {
            int delay = 100 * (i + 1); // 100ms, 200ms, 300ms, 400ms, 500ms

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                ScrollToSelectedItem(listView);
            };
            timer.Start();
        }
    }

    private static void TryScrollToSelectedItem(ListView listView, int attemptsLeft)
    {
        if (attemptsLeft <= 0) return;

        var item = listView.SelectedItem;
        if (item == null) return;

        // Force container generation by calling UpdateLayout
        listView.UpdateLayout();

        // Try to get the container
        var container = listView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
        if (container != null)
        {
            // Container exists, bring it into view (without focus)
            container.BringIntoView();
        }
        else
        {
            // Container doesn't exist yet, try again after a delay
            // Use increasing delays for successive attempts
            int delay = (5 - attemptsLeft + 1) * 50; // 50ms, 100ms, 150ms, 200ms, 250ms

            // Schedule the next attempt with proper delay using DispatcherTimer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                TryScrollToSelectedItem(listView, attemptsLeft - 1);
            };
            timer.Start();
        }
    }
}