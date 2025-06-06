using System.Windows;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Provides an attached property to enable window dragging from any FrameworkElement.
/// </summary>
public static class WindowDragBehavior
{
    public static readonly DependencyProperty IsDragEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsDragEnabled",
            typeof(bool),
            typeof(WindowDragBehavior),
            new PropertyMetadata(false, OnIsDragEnabledChanged));

    public static bool GetIsDragEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDragEnabledProperty);
    }

    public static void SetIsDragEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDragEnabledProperty, value);
    }

    private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            var window = Window.GetWindow(sender as DependencyObject);
            window?.DragMove();
        }
    }

    private static void OnIsDragEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            }
            else
            {
                element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
            }
        }
    }
}