using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Provides a content control that displays an appropriate editor for a property based on its type.
/// This is the base implementation focused on core editing functionality.
/// </summary>
public class PropertyEditor : ContentControl
{
    public static readonly DependencyProperty PropertyItemProperty =
        DependencyProperty.Register(nameof(PropertyItem), typeof(PropertyHierarchyItem), typeof(PropertyEditor),
            new PropertyMetadata(null, OnPropertyItemChanged));

    // Margin constants for consistent spacing
    protected static readonly Thickness CHECKBOX_HEX_MARGIN = new Thickness(6, 0, 3, 0);

    protected static readonly Thickness CONTROL_MARGIN_STANDARD = new Thickness(4, 2, 4, 2);
    protected static readonly Thickness TIP_TEXT_MARGIN = new Thickness(3, 3, 3, 3);

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
    /// Helper to attach focus event for selection
    /// </summary>
    protected void AttachSelectOnFocus(Control control, PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem != null && control != null)
        {
            control.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }
    }

    /// <summary>
    /// Creates the core editor for the property. This method can be used by derived classes.
    /// </summary>
    protected UIElement CreateCoreEditor(PropertyHierarchyItem propertyItem)
    {
        // Handle read-only properties
        if (propertyItem.IsReadOnly)
        {
            return new TextBlock
            {
                Text = propertyItem.FormattedValue ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = CONTROL_MARGIN_STANDARD
            };
        }

        var propertyType = propertyItem.PropertyType;

        // Check for array types first
        if (propertyType != null && propertyType.IsArray)
        {
            return CreateArrayEditor(propertyItem, propertyType);
        }

        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
        {
            return CreateBooleanEditor(propertyItem);
        }
        else if (propertyType != null && propertyType.IsEnum)
        {
            return CreateEnumEditor(propertyItem, propertyType);
        }
        else if (IsIntegerType(propertyType))
        {
            return CreateIntegerEditor(propertyItem);
        }
        else if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(decimal))
        {
            return CreateDecimalEditor(propertyItem, propertyType);
        }
        else if (propertyType == typeof(string))
        {
            return CreateStringEditor(propertyItem);
        }
        else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            return CreateDateTimeEditor(propertyItem);
        }
        else
        {
            return CreateDefaultEditor(propertyItem);
        }
    }

    /// <summary>
    /// Creates a standardized TextBox for property editing
    /// </summary>
    protected TextBox CreateStandardTextBox(string? initialText = null, string? placeholder = null, PropertyHierarchyItem? propertyItem = null)
    {
        var textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxWidth = double.PositiveInfinity
        };

        if (!string.IsNullOrEmpty(initialText))
        {
            textBox.Text = initialText;
        }

        textBox.Tag = placeholder ?? "Enter value";

        try
        {
            textBox.Style = (Style)FindResource("PropertyGridTextBoxWithPlaceholder");
        }
        catch { }

        if (propertyItem != null)
        {
            textBox.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }

        return textBox;
    }

    /// <summary>
    /// Gets a friendly type name for display purposes
    /// </summary>
    protected string GetFriendlyTypeName(Type type)
    {
        return type.Name switch
        {
            "String" => "String",
            "Int32" => "Int32 [integer]",
            "Int64" => "Int64 [long integer]",
            "UInt32" => "UInt32 [unsigned integer]",
            "UInt64" => "UInt64 [unsigned long]",
            "Int16" => "Int16 [short integer]",
            "UInt16" => "UInt16 [unsigned short]",
            "Byte" => "Byte (0-255)",
            "SByte" => "SByte [signed byte] (-128 to 127)",
            "Double" => "Double [decimal number]",
            "Single" => "Single [decimal number]",
            "Decimal" => "Decimal [decimal number]",
            "Boolean" => "Boolean [true/false]",
            "DateTime" => "DateTime [date and time]",
            _ => type.Name.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Called when the PropertyItem changes. Can be overridden by derived classes.
    /// </summary>
    protected virtual void OnPropertyItemChanged(PropertyHierarchyItem? newPropertyItem)
    {
        UpdateEditor(newPropertyItem);
    }

    private UIElement CreateArrayEditor(PropertyHierarchyItem propertyItem, Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null) return new TextBlock { Text = "Invalid array type" };

        var textBox = CreateStandardTextBox(
            FormatArrayValueForEditing(propertyItem.Value),
            $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            propertyItem
        );

        textBox.ToolTip = $"Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons";

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
                    tb.Text = FormatArrayValueForEditing(propertyItem.Value);
                    System.Diagnostics.Debug.WriteLine($"Array parsing error: {ex.Message}");
                }
            }
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        stackPanel.Children.Add(textBox);

        var tipTextBlock = new TextBlock
        {
            Text = $"💡 Enter {GetFriendlyTypeName(elementType)} values separated by commas or semicolons",
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            Opacity = 0.7,
            Margin = TIP_TEXT_MARGIN,
            TextWrapping = TextWrapping.Wrap
        };

        try
        {
            tipTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "PropertyGridForegroundBrush");
        }
        catch
        {
            tipTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
        }

        stackPanel.Children.Add(tipTextBlock);
        return stackPanel;
    }

    private UIElement CreateBooleanEditor(PropertyHierarchyItem propertyItem)
    {
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = CONTROL_MARGIN_STANDARD
        };

        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
        AttachSelectOnFocus(checkBox, propertyItem);
        return checkBox;
    }

    private UIElement CreateDateTimeEditor(PropertyHierarchyItem propertyItem)
    {
        var datePicker = new DatePicker
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD,
            MinWidth = 180
        };

        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);
        AttachSelectOnFocus(datePicker, propertyItem);
        return datePicker;
    }

    private UIElement CreateDecimalEditor(PropertyHierarchyItem propertyItem, Type propertyType)
    {
        var placeholderText = propertyType == typeof(decimal) ? "Enter decimal number (e.g., 123.45)" : "Enter decimal number";
        var textBox = CreateStandardTextBox(null, placeholderText, propertyItem);

        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        };

        textBox.SetBinding(TextBox.TextProperty, binding);
        return textBox;
    }

    private UIElement CreateDefaultEditor(PropertyHierarchyItem propertyItem)
    {
        var textBox = CreateStandardTextBox(propertyItem.FormattedValue, $"Enter {GetFriendlyTypeName(propertyItem.PropertyType)} value", propertyItem);

        textBox.TextChanged += (sender, e) =>
        {
            if (sender is TextBox tb && propertyItem != null)
            {
                try
                {
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

        return textBox;
    }

    private UIElement CreateEnumEditor(PropertyHierarchyItem propertyItem, Type propertyType)
    {
        var comboBox = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD,
            ItemsSource = Enum.GetValues(propertyType),
            MinWidth = 100
        };

        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
        AttachSelectOnFocus(comboBox, propertyItem);
        return comboBox;
    }

    private Grid CreateIntegerEditor(PropertyHierarchyItem propertyItem)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem);

        var checkBox = new CheckBox
        {
            Content = "Hex",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = CHECKBOX_HEX_MARGIN,
            FontSize = 11
        };

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(checkBox, 1);

        grid.Children.Add(textBox);
        grid.Children.Add(checkBox);

        bool isHexadecimal = ShouldDefaultToHex(propertyItem.Value);
        checkBox.IsChecked = isHexadecimal;

        SetupIntegerEditorBinding(textBox, checkBox, propertyItem);

        return grid;
    }

    private UIElement CreateStringEditor(PropertyHierarchyItem propertyItem)
    {
        var textBox = CreateStandardTextBox(null, "Enter text", propertyItem);

        var binding = new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        };

        textBox.SetBinding(TextBox.TextProperty, binding);
        return textBox;
    }

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

    private static bool IsIntegerType(Type? propertyType)
    {
        return propertyType == typeof(int) || propertyType == typeof(int?) ||
               propertyType == typeof(long) || propertyType == typeof(long?) ||
               propertyType == typeof(uint) || propertyType == typeof(uint?) ||
               propertyType == typeof(ulong) || propertyType == typeof(ulong?) ||
               propertyType == typeof(short) || propertyType == typeof(short?) ||
               propertyType == typeof(ushort) || propertyType == typeof(ushort?) ||
               propertyType == typeof(byte) || propertyType == typeof(byte?) ||
               propertyType == typeof(sbyte) || propertyType == typeof(sbyte?);
    }

    private static void OnPropertyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyEditor editor && e.NewValue is PropertyHierarchyItem propertyItem)
        {
            editor.OnPropertyItemChanged(propertyItem);
        }
    }

    private Array ParseArrayValueFromText(string text, Type elementType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.CreateInstance(elementType, 0);
        }

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

    private void SetupIntegerEditorBinding(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        checkBox.Checked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, true);
        checkBox.Unchecked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, false);

        textBox.LostFocus += (s, e) => UpdatePropertyFromIntegerText(textBox, checkBox, propertyItem);

        UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
    }

    private static bool ShouldDefaultToHex(object? value)
    {
        if (value == null) return false;

        try
        {
            var longValue = Convert.ToInt64(value);
            return longValue > 0x80000000;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateEditor(PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem == null)
        {
            Content = null;
            return;
        }

        Content = CreateCoreEditor(propertyItem);
    }

    private void UpdateIntegerDisplay(TextBox textBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        if (propertyItem.Value == null)
        {
            textBox.Text = string.Empty;
            return;
        }

        try
        {
            var longValue = Convert.ToInt64(propertyItem.Value);
            textBox.Text = isHexadecimal ? $"0x{longValue:X}" : longValue.ToString();
        }
        catch
        {
            textBox.Text = propertyItem.Value.ToString() ?? string.Empty;
        }
    }

    private void UpdatePropertyFromIntegerText(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        var text = textBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            propertyItem.Value = null;
            return;
        }

        try
        {
            long longValue;

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = text.Substring(2);
                longValue = Convert.ToInt64(hexValue, 16);
            }
            else
            {
                longValue = Convert.ToInt64(text);
            }

            var targetType = propertyItem.PropertyType;
            if (targetType == typeof(int) || targetType == typeof(int?))
                propertyItem.Value = (int)longValue;
            else if (targetType == typeof(uint) || targetType == typeof(uint?))
                propertyItem.Value = (uint)longValue;
            else if (targetType == typeof(long) || targetType == typeof(long?))
                propertyItem.Value = longValue;
            else if (targetType == typeof(ulong) || targetType == typeof(ulong?))
                propertyItem.Value = (ulong)longValue;
            else if (targetType == typeof(short) || targetType == typeof(short?))
                propertyItem.Value = (short)longValue;
            else if (targetType == typeof(ushort) || targetType == typeof(ushort?))
                propertyItem.Value = (ushort)longValue;
            else if (targetType == typeof(byte) || targetType == typeof(byte?))
                propertyItem.Value = (byte)longValue;
            else if (targetType == typeof(sbyte) || targetType == typeof(sbyte?))
                propertyItem.Value = (sbyte)longValue;
            else
                propertyItem.Value = longValue;

            UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
        }
        catch (Exception)
        {
            UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
        }
    }
}