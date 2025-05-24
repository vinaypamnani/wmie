using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Provides a content control that displays an appropriate editor for a property based on its type.
/// </summary>
public class PropertyEditor : ContentControl
{
    public static readonly DependencyProperty PropertyItemProperty =
        DependencyProperty.Register(nameof(PropertyItem), typeof(PropertyHierarchyItem), typeof(PropertyEditor),
            new PropertyMetadata(null, OnPropertyItemChanged));

    static PropertyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyEditor),
            new FrameworkPropertyMetadata(typeof(PropertyEditor)));
    }

    /// <summary>
    /// Gets or sets the property item to edit.
    /// </summary>
    public PropertyHierarchyItem PropertyItem
    {
        get => (PropertyHierarchyItem)GetValue(PropertyItemProperty);
        set => SetValue(PropertyItemProperty, value);
    }

    private static void OnPropertyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && e.NewValue is PropertyHierarchyItem propertyItem)
        {
            editor.UpdateEditor(propertyItem);
        }
    }

    private void UpdateEditor(PropertyHierarchyItem? propertyItem)
    {
        // Exit if property is read-only or null
        if (propertyItem == null || propertyItem.IsReadOnly)
        {
            // For read-only properties, use a TextBlock
            var textBlock = new TextBlock
            {
                Text = propertyItem?.FormattedValue ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            Content = textBlock;
            return;
        }

        // Handle different property types
        var propertyType = propertyItem.PropertyType;

        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            // Use CheckBox for boolean properties
            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
            Content = checkBox;
        }
        else if (propertyType.IsEnum)
        {
            // Use ComboBox for enum properties
            var comboBox = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0),
                ItemsSource = Enum.GetValues(propertyType)
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
            Content = comboBox;
        }
        else if (propertyType == typeof(int) || propertyType == typeof(double) ||
                propertyType == typeof(float) || propertyType == typeof(decimal))
        {
            // Use a TextBox with numeric validation for numeric types
            var textBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };

            textBox.SetBinding(TextBox.TextProperty, binding);
            Content = textBox;
        }
        else if (propertyType == typeof(string))
        {
            // Use TextBox for string properties
            var textBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };

            textBox.SetBinding(TextBox.TextProperty, binding);
            Content = textBox;
        }
        else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            // Use DatePicker for DateTime properties
            var datePicker = new DatePicker
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };

            var binding = new Binding("Value")
            {
                Source = propertyItem,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);
            Content = datePicker;
        }
        else
        {
            // Default to TextBox for other types
            var textBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0),
                Text = propertyItem.FormattedValue
            };

            textBox.TextChanged += (sender, e) =>
            {
                if (sender is TextBox tb && propertyItem != null)
                {
                    try
                    {
                        // Try to convert the text to the property type
                        var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyItem.PropertyType);
                        if (converter != null && converter.CanConvertFrom(typeof(string)))
                        {
                            propertyItem.Value = converter.ConvertFromString(tb.Text);
                        }
                    }
                    catch
                    {
                        // If conversion fails, don't update the value
                    }
                }
            };

            Content = textBox;
        }
    }
}