using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Provides an attached property to enable ListView scroll passthrough to parent ScrollViewer.
/// When enabled, mouse wheel events over the ListView will be passed to the parent ScrollViewer.
/// </summary>
public static class ListViewScrollPassthroughBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ListViewScrollPassthroughBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);

        while (parent != null && !(parent is T))
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as T;
    }

    private static void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ListView listView)
        {
            // Find the parent ScrollViewer
            var parentScrollViewer = FindParent<ScrollViewer>(listView);
            if (parentScrollViewer != null)
            {
                // Create a new mouse wheel event for the parent ScrollViewer
                var newEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent
                };

                // Raise the event on the parent ScrollViewer
                parentScrollViewer.RaiseEvent(newEventArgs);

                // Mark the original event as handled to prevent ListView from processing it
                e.Handled = true;
            }
        }
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView)
        {
            if ((bool)e.NewValue)
            {
                listView.PreviewMouseWheel += ListView_PreviewMouseWheel;
            }
            else
            {
                listView.PreviewMouseWheel -= ListView_PreviewMouseWheel;
            }
        }
    }
}