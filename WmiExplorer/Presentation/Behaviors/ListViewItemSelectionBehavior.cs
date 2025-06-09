using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Watcher;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior that enables force-selection on clicking an already selected ListViewItem
/// </summary>
public static class ListViewItemSelectionBehavior
{
    public static readonly DependencyProperty EnableForceSelectionProperty =
        DependencyProperty.RegisterAttached(
            "EnableForceSelection",
            typeof(bool),
            typeof(ListViewItemSelectionBehavior),
            new PropertyMetadata(false, OnEnableForceSelectionChanged));

    public static bool GetEnableForceSelection(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableForceSelectionProperty);
    }

    public static void SetEnableForceSelection(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableForceSelectionProperty, value);
    }

    // Helper to find parent ListView
    private static DependencyObject? FindParentListView(DependencyObject child)
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not ListView)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent;
    }

    private static void OnEnableForceSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewItem item)
        {
            if ((bool)e.NewValue)
            {
                item.MouseUp += OnItemMouseUp;
            }
            else
            {
                item.MouseUp -= OnItemMouseUp;
            }
        }
    }

    private static void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.IsSelected && e.LeftButton == MouseButtonState.Released)
        {
            // Only handle single clicks to avoid interfering with double-click behavior
            if (e.ClickCount == 1)
            {
                // Handle different types of view models
                if (item.DataContext is WmiClassViewModel classViewModel)
                {
                    classViewModel.ForceSelection();
                }
                else if (item.DataContext is WmiInstanceViewModel instanceViewModel)
                {
                    instanceViewModel.ForceSelection();
                }
                // Special handling for event items: get parent ListView's DataContext
                else if (item.DataContext is WmiEvent && FindParentListView(item) is ListView listView && listView.DataContext is WmiWatcherViewModel watcherViewModel)
                {
                    watcherViewModel.ForceSelection();
                }
                // Don't mark as handled - this allows double-click to still work
            }
        }
    }
}