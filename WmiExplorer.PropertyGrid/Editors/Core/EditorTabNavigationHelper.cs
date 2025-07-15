using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Editors.Core;

internal static class EditorTabNavigationHelper
{
    public static System.Windows.Controls.TreeViewItem? FindContainerForItem(System.Windows.Controls.ItemsControl parent, object item)
    {
        if (parent == null) return null;
        var container = parent.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.TreeViewItem;
        if (container != null)
            return container;
        foreach (var child in parent.Items)
        {
            var childContainer = parent.ItemContainerGenerator.ContainerFromItem(child) as System.Windows.Controls.ItemsControl;
            if (childContainer != null)
            {
                var result = FindContainerForItem(childContainer, item);
                if (result != null)
                    return result;
            }
        }
        return null;
    }

    public static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                return t;
            var result = FindDescendant<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// Attempts to find the index of the focused element or its ancestor in the list of focusable controls.
    /// </summary>
    public static int FindFocusableControlIndexForElement(IEnumerable<Control> focusableControls, IInputElement? focusedElement)
    {
        if (focusedElement is Control focusedControl)
        {
            int idx = focusableControls.ToList().IndexOf(focusedControl);
            if (idx != -1)
                return idx;
        }
        // Walk up the visual tree to see if the focused element is a descendant of any focusable control
        if (focusedElement is DependencyObject depObj)
        {
            var controls = focusableControls.ToList();
            for (int i = 0; i < controls.Count; i++)
            {
                if (IsDescendantOf(depObj, controls[i]))
                    return i;
            }
        }
        return -1;
    }

    public static void FlattenPropertyItems(object item, List<object> result)
    {
        if (item is PropertyHierarchyItem phi && !phi.IsCategory && phi.Visibility == Visibility.Visible)
        {
            result.Add(phi);
        }
        if (item is PropertyCategoryItem cat)
        {
            foreach (var child in cat.Children)
            {
                FlattenPropertyItems(child, result);
            }
        }
        else if (item is PropertyHierarchyItem phi2)
        {
            foreach (var child in phi2.Children)
            {
                FlattenPropertyItems(child, result);
            }
        }
    }

    /// <summary>
    /// Returns all editable controls (TextBox, ComboBox, CheckBox, etc.) that are enabled, visible, and focusable, in visual order.
    /// </summary>
    public static IEnumerable<Control> GetEditableControls(DependencyObject? root)
    {
        if (root == null) yield break;
        if (root is Control ctrl && IsEditableControlType(ctrl))
        {
            yield return ctrl;
        }
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (var child in GetEditableControls(VisualTreeHelper.GetChild(root, i)))
                yield return child;
        }
    }

    /// <summary>
    /// Returns only leaf focusable controls (controls that do not have any focusable descendants).
    /// </summary>
    public static IEnumerable<Control> GetFocusableControls(DependencyObject? root)
    {
        if (root == null)
            yield break;
        // If this is a focusable control, yield it
        if (root is Control ctrl && ctrl.IsTabStop && ctrl.Focusable && ctrl.IsEnabled && ctrl.Visibility == Visibility.Visible)
            yield return ctrl;
        // Recursively search all descendants, not just direct children
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (var child in GetFocusableControls(VisualTreeHelper.GetChild(root, i)))
                yield return child;
        }
    }

    public static void HandleTabNavigation(IPropertyEditor editor, bool moveBackward, KeyEventArgs e)
    {
        var editableControls = GetEditableControls(editor as DependencyObject).ToList();
        int idx = FindFocusableControlIndexForElement(editableControls, Keyboard.FocusedElement);
        if (editableControls.Count > 0)
        {
            if (idx == -1)
            {
                if (TryMoveToAdjacentEditor(editor, moveBackward))
                {
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                int nextIdx = moveBackward ? idx - 1 : idx + 1;
                if (nextIdx >= 0 && nextIdx < editableControls.Count)
                {
                    editableControls[nextIdx]?.Focus();
                    e.Handled = true;
                    return;
                }
                else
                {
                    if (TryMoveToAdjacentEditor(editor, moveBackward))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
        else
        {
            if (TryMoveToAdjacentEditor(editor, moveBackward))
            {
                e.Handled = true;
                return;
            }
        }
    }

    public static bool TryMoveToAdjacentEditor(IPropertyEditor editor, bool moveBackward)
    {
        // Find the parent TreeView
        DependencyObject? parent = editor as DependencyObject;
        while (parent != null && parent is not System.Windows.Controls.TreeView)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        var treeView = parent as System.Windows.Controls.TreeView;
        if (treeView == null || treeView.ItemsSource == null)
            return false;

        // Flatten the property items (skip categories)
        var items = new List<object>();
        foreach (var root in treeView.ItemsSource)
        {
            FlattenPropertyItems(root, items);
        }
        if (items.Count == 0)
            return false;

        // Find the current property item
        if (editor is not FrameworkElement fe || fe.DataContext is not PropertyHierarchyItem currentItem)
            return false;
        int idx = items.IndexOf(currentItem);
        if (idx == -1)
            return false;

        int nextIdx = moveBackward ? idx - 1 : idx + 1;
        if (nextIdx < 0 || nextIdx >= items.Count)
            return false;

        var nextItem = items[nextIdx];
        // Find the TreeViewItem for the next property
        var tvi = FindContainerForItem(treeView, nextItem);
        if (tvi != null)
        {
            // Find the FrameworkElement that implements IPropertyEditor in the visual tree
            var nextEditor = FindDescendantPropertyEditor(tvi);
            if (nextEditor != null)
            {
                var controls = GetFocusableControls(nextEditor).ToList();
                if (moveBackward)
                {
                    if (controls.Count > 0)
                        controls[^1].Focus();
                    else
                        nextEditor.Focus();
                }
                else
                {
                    if (controls.Count > 0)
                        controls[0].Focus();
                    else
                        nextEditor.Focus();
                }
                return true;
            }
        }
        return false;
    }

    // Helper to find the first FrameworkElement in the visual tree that implements IPropertyEditor
    private static FrameworkElement? FindDescendantPropertyEditor(DependencyObject parent)
    {
        if (parent is FrameworkElement fe && fe is IPropertyEditor)
            return fe;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindDescendantPropertyEditor(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private static bool IsDescendantOf(DependencyObject descendant, DependencyObject ancestor)
    {
        DependencyObject? current = descendant;
        while (current != null)
        {
            if (current == ancestor)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    // Replace IsEditableControlType with a property-based check
    private static bool IsEditableControlType(Control ctrl)
    {
        // Consider any control that is focusable, enabled, visible, and tab stop as editable
        return ctrl.IsTabStop && ctrl.Focusable && ctrl.IsEnabled && ctrl.Visibility == Visibility.Visible;
    }
}