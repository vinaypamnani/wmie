using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

public static class LetterIconHelper
{
    public static readonly DependencyProperty LetterIconBackgroundProperty = DependencyProperty.RegisterAttached(
        "LetterIconBackground", typeof(object), typeof(LetterIconHelper), new PropertyMetadata(null, OnLetterIconBackgroundChanged));

    public static readonly DependencyProperty LetterIconProperty = DependencyProperty.RegisterAttached(
        "LetterIcon", typeof(string), typeof(LetterIconHelper), new PropertyMetadata(null, OnLetterIconChanged));

    public static readonly DependencyProperty LetterIconToolTipProperty = DependencyProperty.RegisterAttached(
        "LetterIconToolTip", typeof(object), typeof(LetterIconHelper), new PropertyMetadata(null, OnLetterIconToolTipChanged));

    public static string GetLetterIcon(DependencyObject element)
    {
        return (string)element.GetValue(LetterIconProperty);
    }

    public static object GetLetterIconBackground(DependencyObject element)
    {
        return element.GetValue(LetterIconBackgroundProperty);
    }

    public static object GetLetterIconToolTip(DependencyObject element)
    {
        return element.GetValue(LetterIconToolTipProperty);
    }

    public static void SetLetterIcon(DependencyObject element, string value)
    {
        element.SetValue(LetterIconProperty, value);
    }

    public static void SetLetterIconBackground(DependencyObject element, object value)
    {
        element.SetValue(LetterIconBackgroundProperty, value);
    }

    public static void SetLetterIconToolTip(DependencyObject element, object value)
    {
        element.SetValue(LetterIconToolTipProperty, value);
    }

    private static void OnLetterIconBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuItem menuItem && menuItem.Icon is ContentControl icon)
        {
            if (e.NewValue is Brush brush)
            {
                icon.Background = brush;
            }
            else if (e.NewValue != null)
            {
                var resource = Application.Current.TryFindResource(e.NewValue);
                if (resource is Brush resolvedBrush)
                {
                    icon.Background = resolvedBrush;
                }
            }
        }
    }

    private static void OnLetterIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuItem menuItem)
        {
            var icon = menuItem.Icon as ContentControl;
            if (icon == null)
            {
                icon = new ContentControl
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Template = (ControlTemplate)Application.Current.FindResource("LetterIconTemplate")
                };
                menuItem.Icon = icon;
            }
            icon.Content = e.NewValue;
            // Set icon tooltip if attached property is set
            var toolTip = GetLetterIconToolTip(menuItem);
            if (toolTip != null)
            {
                icon.ToolTip = toolTip;
            }
        }
    }

    private static void OnLetterIconToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuItem menuItem && menuItem.Icon is ContentControl icon)
        {
            icon.ToolTip = e.NewValue;
        }
    }
}