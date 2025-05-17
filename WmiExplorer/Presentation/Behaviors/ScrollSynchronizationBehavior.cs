using System.Windows;
using System.Windows.Controls;

namespace WmiExplorer.Presentation.Behaviors
{
    /// <summary>
    /// Behavior that synchronizes horizontal scrolling between two scroll viewers
    /// Commonly used to keep headers aligned with content in ListViews
    /// </summary>
    public static class ScrollSynchronizationBehavior
    {
        public static readonly DependencyProperty SynchronizeWithProperty =
            DependencyProperty.RegisterAttached(
                "SynchronizeWith",
                typeof(ScrollViewer),
                typeof(ScrollSynchronizationBehavior),
                new PropertyMetadata(null, OnSynchronizeWithChanged));

        public static ScrollViewer GetSynchronizeWith(DependencyObject obj)
        {
            return (ScrollViewer)obj.GetValue(SynchronizeWithProperty);
        }

        public static void SetSynchronizeWith(DependencyObject obj, ScrollViewer value)
        {
            obj.SetValue(SynchronizeWithProperty, value);
        }

        private static void OnSynchronizeWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                var oldScrollViewer = e.OldValue as ScrollViewer;
                if (oldScrollViewer != null)
                {
                    // Unsubscribe from previous scroll events
                    scrollViewer.ScrollChanged -= OnScrollChanged;
                }

                var newScrollViewer = e.NewValue as ScrollViewer;
                if (newScrollViewer != null)
                {
                    // Subscribe to scroll events
                    scrollViewer.ScrollChanged += OnScrollChanged;
                }
            }
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer sourceScrollViewer && e.HorizontalChange != 0)
            {
                // Only sync horizontal scrolling
                var targetScrollViewer = GetSynchronizeWith(sourceScrollViewer);
                if (targetScrollViewer != null && targetScrollViewer.HorizontalOffset != sourceScrollViewer.HorizontalOffset)
                {
                    targetScrollViewer.ScrollToHorizontalOffset(sourceScrollViewer.HorizontalOffset);
                }
            }
        }
    }
}
