using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WmiExplorer.Presentation.ViewModels.Shared;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior that updates the PropertyGrid whenever an item is clicked,
/// regardless of its current selection state.
/// This eliminates the need for force-selection workarounds.
/// Uses dependency injection pattern similar to AvalonEdit behaviors.
/// Supports: TreeViewItem, ListViewItem, and DataGridRow.
/// </summary>
public static class PropertyGridUpdateBehavior
{
    public static readonly DependencyProperty EnablePropertyGridUpdateProperty =
        DependencyProperty.RegisterAttached(
            "EnablePropertyGridUpdate",
            typeof(bool),
            typeof(PropertyGridUpdateBehavior),
            new PropertyMetadata(false, OnEnablePropertyGridUpdateChanged));

    private static SelectionManager? _selectionManager;

    public static bool GetEnablePropertyGridUpdate(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnablePropertyGridUpdateProperty);
    }

    public static void SetEnablePropertyGridUpdate(DependencyObject obj, bool value)
    {
        obj.SetValue(EnablePropertyGridUpdateProperty, value);
    }

    /// <summary>
    /// Sets the SelectionManager for this behavior (for DI).
    /// </summary>
    public static void SetSelectionManager(SelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    private static void OnEnablePropertyGridUpdateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Support TreeViewItem, ListViewItem, and DataGridRow
        if (d is TreeViewItem or ListViewItem or DataGridRow)
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
        // Only process left-click events - ignore right-clicks and other buttons
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
            return;

        var control = (FrameworkElement)sender;
        var dataContext = control.DataContext;

        // Update the PropertyGrid via SelectionManager
        if (_selectionManager != null)
        {
            _selectionManager.SetSelectedObject(dataContext, updatePropertyGrid: true);

            // Only mark as handled after we've actually processed a left-click
            // This prevents event bubbling to parent TreeViewItems for left-clicks
            // while allowing right-click context menus to work properly
            e.Handled = true;
        }
    }
}