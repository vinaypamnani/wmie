using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WmiExplorer.Presentation.ViewModels;

namespace WmiExplorer.Presentation.Behaviors
{
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
                    // Don't mark as handled - this allows double-click to still work
                }
            }
        }

        public static bool GetEnableForceSelection(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableForceSelectionProperty);
        }

        public static void SetEnableForceSelection(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableForceSelectionProperty, value);
        }
    }
}