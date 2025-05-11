using System.Windows;
using System.Windows.Controls;
using TreeView = System.Windows.Controls.TreeView;

namespace WmiExplorer.Presentation.Behaviors
{
    public static class TreeViewAutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(TreeViewAutoScrollBehavior),
                new UIPropertyMetadata(false, OnAutoScrollChanged));

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView treeView && (bool)e.NewValue)
            {
                treeView.SelectedItemChanged += (s, args) =>
                {
                    if (treeView.ItemContainerGenerator.ContainerFromItem(args.NewValue) is TreeViewItem item)
                    {
                        item.BringIntoView();
                    }
                };
            }
        }

        public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);

        public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);
    }
}