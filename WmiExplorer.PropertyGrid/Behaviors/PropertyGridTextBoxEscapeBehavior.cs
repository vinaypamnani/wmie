using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WmiExplorer.PropertyGrid.Behaviors;

/// <summary>
/// Provides an attached behavior to handle Escape key press in PropertyGrid TextBox controls.
/// </summary>
public static class PropertyGridTextBoxEscapeBehavior
{
    public static readonly DependencyProperty EnableEscapeClearProperty =
        DependencyProperty.RegisterAttached(
            "EnableEscapeClear",
            typeof(bool),
            typeof(PropertyGridTextBoxEscapeBehavior),
            new PropertyMetadata(false, OnEnableEscapeClearChanged));

    public static bool GetEnableEscapeClear(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableEscapeClearProperty);
    }

    public static void SetEnableEscapeClear(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableEscapeClearProperty, value);
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

    private static void OnEnableEscapeClearChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.KeyDown += TextBox_KeyDown;
            }
            else
            {
                textBox.KeyDown -= TextBox_KeyDown;
            }
        }
    }

    private static void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is TextBox textBox)
        {
            // Find the PropertyGrid and execute its ClearSearchCommand
            var propertyGrid = FindParent<PropertyGrid>(textBox);
            if (propertyGrid?.ClearSearchCommand?.CanExecute(null) == true)
            {
                propertyGrid.ClearSearchCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}