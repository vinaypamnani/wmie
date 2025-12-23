using System.Windows;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

/// <summary>
/// Helper for marking MenuItems that should be treated specially in styles (e.g., those with Hyperlink headers).
/// </summary>
public static class MenuItemHelper
{
    public static readonly DependencyProperty IsHyperlinkMenuItemProperty =
        DependencyProperty.RegisterAttached(
            "IsHyperlinkMenuItem",
            typeof(bool),
            typeof(MenuItemHelper),
            new FrameworkPropertyMetadata(false));

    public static bool GetIsHyperlinkMenuItem(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsHyperlinkMenuItemProperty);
    }

    public static void SetIsHyperlinkMenuItem(DependencyObject obj, bool value)
    {
        obj.SetValue(IsHyperlinkMenuItemProperty, value);
    }
}