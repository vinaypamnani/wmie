using System.Windows;

namespace WmiExplorer.PropertyGrid.Behaviors;

public static class SuppressRequestBringIntoViewBehavior
{
    public static readonly DependencyProperty SuppressBringIntoViewProperty =
        DependencyProperty.RegisterAttached(
            "SuppressBringIntoView",
            typeof(bool),
            typeof(SuppressRequestBringIntoViewBehavior),
            new UIPropertyMetadata(false, OnSuppressBringIntoViewChanged));

    public static bool GetSuppressBringIntoView(DependencyObject obj)
                        => (bool)obj.GetValue(SuppressBringIntoViewProperty);

    public static void SetSuppressBringIntoView(DependencyObject obj, bool value)
        => obj.SetValue(SuppressBringIntoViewProperty, value);

    private static void Element_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        e.Handled = true; // Suppress auto-scroll
    }

    private static void OnSuppressBringIntoViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
                element.RequestBringIntoView += Element_RequestBringIntoView;
            else
                element.RequestBringIntoView -= Element_RequestBringIntoView;
        }
    }
}