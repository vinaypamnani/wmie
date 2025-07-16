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
        // Support TreeViewItem, ListViewItem, and DataGridRow
        if (d is TreeViewItem or ListViewItem or DataGridRow)
        {
            var control = (Control)d;
            if ((bool)e.NewValue)
            {
                control.MouseUp += OnItemMouseUp;
                control.PreviewKeyDown += OnItemPreviewKeyDown;
            }
            else
            {
                control.MouseUp -= OnItemMouseUp;
                control.PreviewKeyDown -= OnItemPreviewKeyDown;
            }
        }
    }

    private static void OnItemPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Only handle <Space> key
        if (e.Key != Key.Space)
            return;

        var control = (FrameworkElement)sender;
        var dataContext = control.DataContext;

        // Check if PropertyGrid should be updated
        bool updatePropertyGrid = GetUpdatePropertyGrid(control);

        // Update selection via SelectionManager (which handles IsSelected management)
        if (_selectionManager != null)
        {
            _selectionManager.SetSelectedObject(dataContext, updatePropertyGrid);
            e.Handled = true; // Prevent default behavior
        }
    }

    private static void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Single click only
        if (e.ClickCount != 1)
            return;

        var control = (FrameworkElement)sender;
        var dataContext = control.DataContext;

        // Determine which mouse buttons to process for selectionbased on control type
        bool processLeftClick = e.ChangedButton == MouseButton.Left;
        bool processRightClick = e.ChangedButton == MouseButton.Right && control is not TreeViewItem;

        if (!processLeftClick && !processRightClick)
            return;

        // Check if PropertyGrid should be updated
        bool updatePropertyGrid = GetUpdatePropertyGrid(control);

        // Update selection via SelectionManager (which handles IsSelected management)
        if (_selectionManager != null)
        {
            // SelectionManager will handle IsSelected properties first, then optionally PropertyGrid update
            _selectionManager.SetSelectedObject(dataContext, updatePropertyGrid);

            // Only Handle left-clicks for all controls to prevent bubbling and allow right-click context menus
            if (processLeftClick)
            {
                e.Handled = true;
            }
        }
    }
}