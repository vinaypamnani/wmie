using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Provides an attached behavior to clear a TextBox when the clear button is clicked or Escape key is pressed.
/// </summary>
public static class TextBoxClearButtonBehavior
{
    public static readonly DependencyProperty EnableClearButtonProperty =
        DependencyProperty.RegisterAttached(
            "EnableClearButton",
            typeof(bool),
            typeof(TextBoxClearButtonBehavior),
            new PropertyMetadata(false, OnEnableClearButtonChanged));

    public static bool GetEnableClearButton(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableClearButtonProperty);
    }

    public static void SetEnableClearButton(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableClearButtonProperty, value);
    }

    private static void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        // Find the parent TextBox and clear its text
        if (sender is Button button)
        {
            var textBox = FindParent<TextBox>(button);
            if (textBox != null)
            {
                textBox.Clear();
            }
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        DependencyObject? parent = child;
        while (parent != null && !(parent is T))
        {
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return parent as T;
    }

    private static void OnEnableClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.Loaded += TextBox_Loaded;
                textBox.KeyDown += TextBox_KeyDown;
            }
            else
            {
                textBox.Loaded -= TextBox_Loaded;
                textBox.KeyDown -= TextBox_KeyDown;
            }
        }
    }

    private static void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is TextBox textBox)
        {
            textBox.Clear();
            e.Handled = true;
        }
    }

    private static void TextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var template = textBox.Template;
            if (template != null)
            {
                var clearButton = template.FindName("PART_ClearButton", textBox) as Button;
                if (clearButton != null)
                {
                    clearButton.Click -= ClearButton_Click;
                    clearButton.Click += ClearButton_Click;
                }
            }
        }
    }
}