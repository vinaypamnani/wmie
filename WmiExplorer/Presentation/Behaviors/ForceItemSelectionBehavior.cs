using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior that enables force-selection on clicking an already selected item (TreeViewItem or ListViewItem)
/// </summary>
public static class ForceItemSelectionBehavior
{
    public static readonly DependencyProperty EnableForceSelectionProperty =
        DependencyProperty.RegisterAttached(
            "EnableForceSelection",
            typeof(bool),
            typeof(ForceItemSelectionBehavior),
            new PropertyMetadata(false, OnEnableForceSelectionChanged));

    public static bool GetEnableForceSelection(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableForceSelectionProperty);
    }

    public static void SetEnableForceSelection(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableForceSelectionProperty, value);
    }

    // Helper to find parent ListView (only needed for WmiEvent handling)
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
        // Support both TreeViewItem and ListViewItem
        if (d is TreeViewItem or ListViewItem)
        {
            var control = (Control)d;
            if ((bool)e.NewValue)
            {
                control.MouseUp += OnItemMouseUp;
            }
            else
            {
                control.MouseUp -= OnItemMouseUp;
            }
        }
    }

    private static void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Released || e.ClickCount != 1)
            return;

        var isSelected = sender switch
        {
            TreeViewItem treeItem => treeItem.IsSelected,
            ListViewItem listItem => listItem.IsSelected,
            _ => false
        };

        if (!isSelected)
            return;

        var dataContext = ((FrameworkElement)sender).DataContext;

        // Handle different types of view models
        switch (dataContext)
        {
            case WmiNamespaceViewModel namespaceViewModel:
                namespaceViewModel.ForceSelection();
                break;

            case WmiClassViewModel classViewModel:
                classViewModel.ForceSelection();
                break;

            case WmiInstanceViewModel instanceViewModel:
                instanceViewModel.ForceSelection();
                break;

            case WmiEvent when sender is ListViewItem listItem:
                // Special handling for event items: get parent ListView's DataContext
                if (FindParentListView(listItem) is ListView listView &&
                    listView.DataContext is WatcherTabViewModel watcherViewModel)
                {
                    watcherViewModel.ForceSelection();
                }
                break;
        }

        // Don't mark as handled - this allows double-click to still work
    }
}