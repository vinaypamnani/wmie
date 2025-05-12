using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.Presentation.ViewModels;
using TreeView = System.Windows.Controls.TreeView;

namespace WmiExplorer.Presentation.Behaviors
{
    public static class TreeViewSelectedItemBehavior
    {
        public static readonly DependencyProperty EnableForceSelectionProperty =
            DependencyProperty.RegisterAttached(
                "EnableForceSelection",
                typeof(bool),
                typeof(TreeViewSelectedItemBehavior),
                new PropertyMetadata(false, OnEnableForceSelectionChanged));

        public static readonly DependencyProperty SelectedItemProperty =
                    DependencyProperty.RegisterAttached(
                "SelectedItem",
                typeof(object),
                typeof(TreeViewSelectedItemBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        static TreeViewSelectedItemBehavior()
        {
            EventManager.RegisterClassHandler(typeof(TreeViewItem),
                TreeViewItem.SelectedEvent,
                new RoutedEventHandler(OnTreeViewItemSelected));
        }

        private static TreeView? FindTreeView(DependencyObject item)
        {
            while (item != null && item is not TreeView)
                item = VisualTreeHelper.GetParent(item);
            return item as TreeView;
        }

        private static void OnEnableForceSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeViewItem item)
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
            if (sender is TreeViewItem item && item.IsSelected && e.LeftButton == MouseButtonState.Released)
            {
                // Only handle single clicks to avoid interfering with double-click behavior
                if (e.ClickCount == 1 && item.DataContext is WmiNamespaceViewModel viewModel)
                {
                    viewModel.ForceSelection();
                    // Don't mark as handled - this allows double-click to still work
                }
            }
        }

        private static void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item)
            {
                var tree = FindTreeView(item);
                if (tree != null)
                {
                    SetSelectedItem(tree, item.DataContext);
                }
            }
        }

        public static bool GetEnableForceSelection(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableForceSelectionProperty);
        }

        public static object GetSelectedItem(DependencyObject obj)
        {
            return obj.GetValue(SelectedItemProperty);
        }

        public static void SetEnableForceSelection(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableForceSelectionProperty, value);
        }

        public static void SetSelectedItem(DependencyObject obj, object value)
        {
            obj.SetValue(SelectedItemProperty, value);
        }
    }
}