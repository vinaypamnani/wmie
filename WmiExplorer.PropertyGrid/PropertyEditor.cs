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

    /// <summary>
    /// Creates an editor for array properties supporting comma/semicolon-separated values
    /// </summary>
    private void CreateArrayEditor(PropertyHierarchyItem propertyItem, Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null) return;

        var stackPanel = new StackPanel();

        // Create a TextBox for comma/semicolon-separated input
        var textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0),
            Text = FormatArrayValueForEditing(propertyItem.Value),
            Tag = $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            Style = (Style)Application.Current.FindResource("PropertyGridTextBoxWithPlaceholder"),
            ToolTip = $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons"
        };

        textBox.LostFocus += (sender, e) =>
        {
            if (sender is TextBox tb)
            {
                try
                {
                    var arrayValue = ParseArrayValueFromText(tb.Text, elementType);
                    propertyItem.Value = arrayValue;
                }
                catch (Exception ex)
                {
                    // Reset to original value if parsing fails
                    tb.Text = FormatArrayValueForEditing(propertyItem.Value);
                    System.Diagnostics.Debug.WriteLine($"Array parsing error: {ex.Message}");
                }
            }
        };

        stackPanel.Children.Add(textBox);

        // Add a small help text
        var helpText = new TextBlock
        {
            Text = "Tip: Separate values with commas or semicolons",
            FontSize = 10,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(3, 2, 0, 0)
        };
        stackPanel.Children.Add(helpText);

        Content = stackPanel;
    }

    /// <summary>
    /// Formats an array value for text editing
    /// </summary>
    private string FormatArrayValueForEditing(object? value)
    {
        if (value is Array array)
        {
            var values = new List<string>();
            for (int i = 0; i < array.Length; i++)
            {
                var item = array.GetValue(i);
                values.Add(item?.ToString() ?? "");
            }
            return string.Join(", ", values);
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets a friendly type name for display purposes
    /// </summary>
    private string GetFriendlyTypeName(Type type)
    {
        return type.Name switch
        {
            "String" => "string",
            "Int32" => "integer",
            "Int64" => "long",
            "Double" => "double",
            "Single" => "float",
            "Boolean" => "boolean",
            "DateTime" => "date/time",
            _ => type.Name.ToLowerInvariant()
        };
    }

    private static void OnPropertyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && e.NewValue is PropertyHierarchyItem propertyItem)
        {
            editor.UpdateEditor(propertyItem);
        }
    }

    /// <summary>
    /// Parses comma/semicolon-separated text into an array of the specified element type
    /// </summary>
    private Array ParseArrayValueFromText(string text, Type elementType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.CreateInstance(elementType, 0);
        }

        // Split by comma or semicolon, remove empty entries
        var separators = new[] { ',', ';' };
        var stringValues = text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .ToArray();

        var array = Array.CreateInstance(elementType, stringValues.Length);
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(elementType);

        for (int i = 0; i < stringValues.Length; i++)
        {
            try
            {
                object? convertedValue = null;

                if (elementType == typeof(string))
                {
                    convertedValue = stringValues[i];
                }
                else if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    convertedValue = converter.ConvertFromString(stringValues[i]);
                }
                else
                {
                    // Try direct conversion for common types
                    convertedValue = Convert.ChangeType(stringValues[i], elementType);
                }

                array.SetValue(convertedValue, i);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot convert '{stringValues[i]}' to {elementType.Name}: {ex.Message}", ex);
            }
        }

        return array;
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

        // Check for array types first
        if (propertyType != null && propertyType.IsArray)
        {
            CreateArrayEditor(propertyItem, propertyType);
            return;
        }
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
        else if (propertyType != null && propertyType.IsEnum)
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