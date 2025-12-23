using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WmiExplorer.Presentation.ViewModels.Shared;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior that handles item selection whenever an item is clicked,
/// regardless of its current selection state. Provides granular control over
/// selection forcing and PropertyGrid updates through separate dependency properties.
/// Uses dependency injection pattern similar to AvalonEdit behaviors.
/// Supports: TreeViewItem, ListViewItem, and DataGridRow.
///
/// Selection behavior:
/// - Left-click: Triggers selection for all supported control types
/// - Right-click: Triggers selection only for ListViewItem and DataGridRow (not TreeViewItem)
///   This prevents right-click selection bubbling issues in TreeView hierarchies
///
/// Note: IsSelected property management is handled by SelectionManager to ensure
/// proper order of operations (local OnIsSelectedChanged actions before PropertyGrid updates).
/// </summary>
public static class ItemSelectionBehavior
{
    public static readonly DependencyProperty EnableForceSelectionProperty =
        DependencyProperty.RegisterAttached(
            "EnableForceSelection",
            typeof(bool),
            typeof(ItemSelectionBehavior),
            new PropertyMetadata(false, OnEnableForceSelectionChanged));

    public static readonly DependencyProperty UpdatePropertyGridProperty =
        DependencyProperty.RegisterAttached(
            "UpdatePropertyGrid",
            typeof(bool),
            typeof(ItemSelectionBehavior),
            new PropertyMetadata(true));

    // Default to true for backward compatibility

    private static SelectionManager? _selectionManager;

    public static bool GetEnableForceSelection(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableForceSelectionProperty);
    }

    public static bool GetUpdatePropertyGrid(DependencyObject obj)
    {
        return (bool)obj.GetValue(UpdatePropertyGridProperty);
    }

    public static void SetEnableForceSelection(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableForceSelectionProperty, value);
    }

    /// <summary>
    /// Sets the SelectionManager for this behavior (for DI).
    /// </summary>
    public static void SetSelectionManager(SelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public static void SetUpdatePropertyGrid(DependencyObject obj, bool value)
    {
        obj.SetValue(UpdatePropertyGridProperty, value);
    }

    private static void OnEnableForceSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Attach/detach handlers for all supported item types
        if (d is TreeViewItem || d is ListViewItem || d is DataGridRow)
        {
            var control = (Control)d;
            if ((bool)e.NewValue)
            {
                control.KeyDown += OnItemKeyDown;
                control.MouseUp += OnItemMouseUp;
                control.PreviewMouseRightButtonDown += OnItemPreviewMouseRightButtonDown;
            }
            else
            {
                control.KeyDown -= OnItemKeyDown;
                control.MouseUp -= OnItemMouseUp;
                control.PreviewMouseRightButtonDown -= OnItemPreviewMouseRightButtonDown;
            }
        }
    }

    private static void OnItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
            return;

        if (sender is TreeViewItem or ListViewItem or DataGridRow)
        {
            SelectItem((FrameworkElement)sender);
            e.Handled = true;
        }
    }

    private static void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Only handle left-click for all supported item types
        if (e.ClickCount != 1 || e.ChangedButton != MouseButton.Left)
            return;

        SelectItem((FrameworkElement)sender);
        e.Handled = true;
    }

    private static void OnItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Handle right-click selection for all supported item types
        SelectItem((FrameworkElement)sender);
        // Do NOT set e.Handled = true; so context menu will show
    }

    private static void SelectItem(FrameworkElement control)
    {
        // Set IsSelected = true for supported controls to ensure UI state is always consistent
        switch (control)
        {
            case TreeViewItem tvi:
                tvi.IsSelected = true;
                break;
            case ListViewItem lvi:
                lvi.IsSelected = true;
                break;
            case DataGridRow dgr:
                dgr.IsSelected = true;
                break;
        }
        bool updatePropertyGrid = GetUpdatePropertyGrid(control);
        var dataContext = control.DataContext;
        _selectionManager?.SetSelectedObject(dataContext, updatePropertyGrid);
    }
}