using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Converter to calculate MaxWidth for controls based on available space
/// </summary>
public class MaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double actualWidth && actualWidth > 0)
        {
            // Parse the parameter as the width to subtract (column0 width + margins/padding)
            double widthToSubtract = 2.0; // Default fallback
            if (parameter != null && double.TryParse(parameter.ToString(), out double paramValue))
            {
                widthToSubtract = paramValue;
            }

            return Math.Max(100, actualWidth - widthToSubtract); // Minimum 100px
        }
        return 300.0; // Fallback width
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Static utility class providing common functionality for property editors.
/// This centralizes reusable editor creation and styling logic.
/// </summary>
public static class PropertyEditorUtils
{
    public static readonly Thickness CHECKBOX_HEX_MARGIN = new Thickness(6, 0, 3, 0);

    // Margin constants for consistent spacing across all editors
    public static readonly Thickness CONTROL_MARGIN_STANDARD = new Thickness(4, 2, 4, 2);

    public static readonly Thickness TIP_TEXT_MARGIN = new Thickness(3, 3, 3, 3);

    /// <summary>
    /// Applies MaxWidth constraint to any FrameworkElement based on parent container
    /// </summary>
    public static void ApplyMaxWidthConstraint(FrameworkElement element, FrameworkElement? parentContainer = null, double widthToSubtract = 20, bool forceApply = false)
    {
        // Check if element already has a MaxWidth binding (to avoid double-binding)
        if (!forceApply && BindingOperations.GetBinding(element, FrameworkElement.MaxWidthProperty) != null)
        {
            return; // Already has a MaxWidth binding, don't override
        }

        if (parentContainer != null)
        {
            var maxWidthConverter = new MaxWidthConverter();
            element.SetBinding(FrameworkElement.MaxWidthProperty,
                new Binding("ActualWidth") { Source = parentContainer, Converter = maxWidthConverter, ConverterParameter = widthToSubtract });
        }
        else
        {
            // If no parent container, try to use a parent element
            element.SetBinding(FrameworkElement.MaxWidthProperty,
                new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Grid), 1), Converter = new MaxWidthConverter(), ConverterParameter = widthToSubtract });
        }
    }

    /// <summary>
    /// Applies standard configuration to a FrameworkElement for property editing
    /// </summary>
    public static void ApplyStandardEditorConfiguration(FrameworkElement element, PropertyHierarchyItem propertyItem, bool applyMaxWidth = true)
    {
        element.VerticalAlignment = VerticalAlignment.Center;
        element.HorizontalAlignment = HorizontalAlignment.Stretch;
        element.Margin = CONTROL_MARGIN_STANDARD;

        if (applyMaxWidth)
        {
            ApplyMaxWidthConstraint(element);
        }

        if (element is Control control)
        {
            AttachSelectOnFocus(control, propertyItem);
        }
    }

    /// <summary>
    /// Helper to attach focus event for selection
    /// </summary>
    public static void AttachSelectOnFocus(Control control, PropertyHierarchyItem? propertyItem)
    {
        if (propertyItem != null && control != null)
        {
            control.GotFocus += (s, e) => propertyItem.IsSelected = true;
        }
    }

    /// <summary>
    /// Clears integer validation error state from a TextBox
    /// </summary>
    public static void ClearIntegerValidationError(TextBox textBox)
    {
        textBox.ClearValue(Control.BorderBrushProperty);
        textBox.ClearValue(Control.BorderThicknessProperty);
        textBox.ClearValue(Control.ToolTipProperty);
        textBox.ClearValue(Control.BackgroundProperty);
    }

    /// <summary>
    /// Clears validation error state from a TextBox
    /// </summary>
    public static void ClearValidationError(TextBox textBox, Brush originalBorderBrush, object originalToolTip)
    {
        // Restore original border
        textBox.BorderBrush = originalBorderBrush;
        textBox.BorderThickness = new Thickness(1);

        // Restore original tooltip
        textBox.ToolTip = originalToolTip;

        // Clear error background
        textBox.ClearValue(Control.BackgroundProperty);
    }

    /// <summary>
    /// Convenience method to clear validation errors without needing original values
    /// </summary>
    public static void ClearValidationError(TextBox textBox)
    {
        // Restore default styling
        textBox.ClearValue(Control.BorderBrushProperty);
        textBox.ClearValue(Control.BorderThicknessProperty);
        textBox.ClearValue(Control.ToolTipProperty);
        textBox.ClearValue(Control.BackgroundProperty);
    }

    /// <summary>
    /// Creates an array editor with input validation and tips
    /// </summary>
    public static StackPanel CreateArrayEditor(PropertyHierarchyItem propertyItem, Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null)
        {
            return new StackPanel
            {
                Children = { new TextBlock { Text = "Invalid array type" } }
            };
        }

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
                    // Store the original value for comparison
                    var originalValue = propertyItem.Value;

                    var arrayValue = ParseArrayValueFromText(tb.Text, elementType);
                    propertyItem.Value = arrayValue;

                    // Check if the array was actually changed
                    bool arrayChanged = !AreArraysEqual(originalValue as Array, arrayValue);

                    if (arrayChanged)
                    {
                        // Show success state for modified arrays
                        ShowValidationSuccess(tb);
                    }
                    else
                    {
                        // Clear any previous styling if array unchanged
                        ClearValidationError(tb);
                    }
                }
                catch (Exception ex)
                {
                    tb.Text = FormatArrayValueForEditing(propertyItem.Value);
                    ShowValidationError(tb, $"Array parsing error: {ex.Message}");
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
            tipTextBlock.Foreground = Brushes.Gray;
        }

        stackPanel.Children.Add(tipTextBlock);
        return stackPanel;
    }

    /// <summary>
    /// Creates a standardized CheckBox t
    /// <summary>
    /// Creates a standardized CheckBox for boolean property editing
    /// </summary>

    // ===== SPECIALIZED EDITOR CREATION UTILITIES =====

    public static CheckBox CreateBooleanEditor(PropertyHierarchyItem propertyItem)
    {
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = CONTROL_MARGIN_STANDARD
        };

        var binding = CreateStandardPropertyBinding(propertyItem);
        checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
        AttachSelectOnFocus(checkBox, propertyItem);

        return checkBox;
    }

    /// <summary>
    /// Creates a standardized DatePicker for DateTime property editing
    /// </summary>
    public static DatePicker CreateDateTimeEditor(PropertyHierarchyItem propertyItem)
    {
        var datePicker = new DatePicker
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD,
            MinWidth = 180
        };

        ApplyMaxWidthConstraint(datePicker);

        var binding = CreateStandardPropertyBinding(propertyItem);
        datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);
        AttachSelectOnFocus(datePicker, propertyItem);

        return datePicker;
    }

    /// <summary>
    /// Creates a standardized TextBox for decimal/floating-point property editing
    /// </summary>
    public static TextBox CreateDecimalEditor(PropertyHierarchyItem propertyItem, Type propertyType)
    {
        var placeholderText = propertyType == typeof(decimal) ? "Enter decimal number (e.g., 123.45)" : "Enter decimal number";
        var textBox = CreateStandardTextBox(null, placeholderText, propertyItem);

        var binding = CreateStandardPropertyBinding(propertyItem, UpdateSourceTrigger.LostFocus);
        textBox.SetBinding(TextBox.TextProperty, binding);

        return textBox;
    }

    /// <summary>
    /// Creates a standardized ComboBox for enum property editing
    /// </summary>
    public static ComboBox CreateEnumEditor(PropertyHierarchyItem propertyItem, Type enumType)
    {
        var comboBox = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD,
            ItemsSource = Enum.GetValues(enumType),
            MinWidth = 100
        };

        ApplyMaxWidthConstraint(comboBox);

        var binding = CreateStandardPropertyBinding(propertyItem);
        comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
        AttachSelectOnFocus(comboBox, propertyItem);

        return comboBox;
    }

    /// <summary>
    /// Creates a standard Grid layout with main content and action button
    /// </summary>
    public static Grid CreateGridWithActionButton(UIElement mainContent, UIElement actionButton, double buttonWidth = 80)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(mainContent, 0);
        grid.Children.Add(mainContent);

        if (actionButton != null)
        {
            if (actionButton is FrameworkElement fe)
            {
                fe.Margin = new Thickness(8, 0, 0, 0);
                fe.VerticalAlignment = VerticalAlignment.Center;
                if (fe.Width.Equals(double.NaN) && buttonWidth > 0)
                {
                    fe.Width = buttonWidth;
                }
            }

            Grid.SetColumn(actionButton, 1);
            grid.Children.Add(actionButton);
        }

        // Apply MaxWidth constraint to main content accounting for button width
        if (mainContent is FrameworkElement mainElement)
        {
            ApplyMaxWidthConstraint(mainElement, grid, buttonWidth + 16); // Account for button width + margins
        }

        return grid;
    }

    /// <summary>
    /// Creates a complete integer editor with hex/decimal support
    /// </summary>
    public static Grid CreateIntegerEditor(PropertyHierarchyItem propertyItem)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = CONTROL_MARGIN_STANDARD
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        ApplyMaxWidthConstraint(grid);

        var textBox = CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem, new Thickness(0));

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

        bool isHexadecimal = ShouldDefaultToHex(propertyItem?.Value);
        checkBox.IsChecked = isHexadecimal;

        if (propertyItem != null)
        {
            SetupIntegerEditorBinding(textBox, checkBox, propertyItem);
        }

        return grid;
    }

    /// <summary>
    /// Creates a read-only TextBlock for displaying property values
    /// </summary>
    public static TextBlock CreateReadOnlyEditor(PropertyHierarchyItem propertyItem)
    {
        return new TextBlock
        {
            Text = propertyItem.FormattedValue ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = CONTROL_MARGIN_STANDARD
        };
    }

    /// <summary>
    /// Creates a standard twp
    /// <summary>
    /// Creates a standard two-way binding for property values
    /// </summary>

    // ===== BINDING CREATION UTILITIES =====

    public static Binding CreateStandardPropertyBinding(PropertyHierarchyItem propertyItem, UpdateSourceTrigger trigger = UpdateSourceTrigger.PropertyChanged)
    {
        return new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = trigger,
            ValidatesOnExceptions = true
        };
    }

    /// <summary>
    /// Creates a standardized TextBox for property editing with consistent styling and behavior
    /// </summary>
    public static TextBox CreateStandardTextBox(string? initialText = null, string? placeholder = null, PropertyHierarchyItem? propertyItem = null, Thickness? margin = null, Action<TextBox, PropertyHierarchyItem, Brush, object>? customValidation = null)
    {
        var textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = margin ?? CONTROL_MARGIN_STANDARD,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Apply MaxWidth constraint using the generic method
        ApplyMaxWidthConstraint(textBox);

        if (!string.IsNullOrEmpty(initialText))
        {
            textBox.Text = initialText;
        }

        textBox.Tag = placeholder ?? "Enter value";

        // Try to apply style if available
        try
        {
            if (Application.Current.TryFindResource("PropertyGridTextBoxWithPlaceholder") is Style style)
            {
                textBox.Style = style;
            }
        }
        catch
        {
            // Style not available, continue without it
        }

        // Attach selection behavior if property item provided
        AttachSelectOnFocus(textBox, propertyItem);

        // Add validation behavior if property item provided
        if (propertyItem != null)
        {
            AddValidationBehavior(textBox, propertyItem, customValidation);
        }

        return textBox;
    }

    /// <summary>
    /// Creates a standardized TextBox for string property editing
    /// </summary>
    public static TextBox CreateStringEditor(PropertyHierarchyItem propertyItem)
    {
        var textBox = CreateStandardTextBox(null, "Enter text", propertyItem);

        var binding = CreateStandardPropertyBinding(propertyItem, UpdateSourceTrigger.LostFocus);
        textBox.SetBinding(TextBox.TextProperty, binding);

        return textBox;
    }

    /// <summary>
    /// Formats an array van
    /// <summary>
    /// Formats an array value for text editing
    /// </summary>

    // ===== ARRAY HANDLING UTILITIES =====

    public static string FormatArrayValueForEditing(object? value)
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
    /// Gets a friendly display name for a type
    /// </summary>
    public static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(bool)) return "Boolean";
        if (type == typeof(byte)) return "Byte";
        if (type == typeof(sbyte)) return "SByte";
        if (type == typeof(short)) return "Int16";
        if (type == typeof(ushort)) return "UInt16";
        if (type == typeof(int)) return "Int32";
        if (type == typeof(uint)) return "UInt32";
        if (type == typeof(long)) return "Int64";
        if (type == typeof(ulong)) return "UInt64";
        if (type == typeof(float)) return "Single";
        if (type == typeof(double)) return "Double";
        if (type == typeof(decimal)) return "Decimal";
        if (type == typeof(string)) return "String";
        if (type == typeof(DateTime)) return "DateTime";

        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetFriendlyTypeName(type.GetGenericArguments()[0]) + "?";
        }

        return type.Name;
    }

    /// <summary>
    /// Checks if a type is an i
    /// <summary>
    /// Checks if a type is an integer type (public version)
    /// </summary>

    // ===== INTEGER VALIDATION UTILITIES =====

    public static bool IsIntegerType(Type? propertyType)
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

    /// <summary>
    /// Parses array value from text input
    /// </summary>
    public static Array ParseArrayValueFromText(string text, Type elementType)
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
                    convertedValue = System.Convert.ChangeType(stringValues[i], elementType);
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

    /// <summary>
    /// Determines if a value should default to hexadecimal display
    /// </summary>
    public static bool ShouldDefaultToHex(object? value)
    {
        if (value == null) return false;

        try
        {
            var longValue = System.Convert.ToInt64(value);
            return longValue > 0x80000000;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shows integer validation error state on a TextBox
    /// </summary>
    public static void ShowIntegerValidationError(TextBox textBox, string errorMessage)
    {
        textBox.BorderBrush = Brushes.Red;
        textBox.BorderThickness = new Thickness(2);
        textBox.ToolTip = $"❌ Validation Error: {errorMessage}";
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
    }

    /// <summary>
    /// Shows integer validation success state on a TextBox for modified values
    /// </summary>
    public static void ShowIntegerValidationSuccess(TextBox textBox, string successMessage = "Value modified")
    {
        textBox.BorderBrush = Brushes.Green;
        textBox.BorderThickness = new Thickness(2);
        textBox.ToolTip = $"✅ {successMessage}\n\nPress Escape to reset to original value.";
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0));
    }

    /// <summary>
    /// Shows validation error state on a TextBox
    /// </summary>
    public static void ShowValidationError(TextBox textBox, string errorMessage)
    {
        // Set error border
        textBox.BorderBrush = Brushes.Red;
        textBox.BorderThickness = new Thickness(2);

        // Set error tooltip
        textBox.ToolTip = $"❌ Validation Error: {errorMessage}\n\nPress Escape to reset to original value.";

        // Optional: Add background tint to make error more visible
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
    }

    /// <summary>
    /// Shows validation success state on a TextBox for modified values
    /// </summary>
    public static void ShowValidationSuccess(TextBox textBox, string successMessage = "Value modified")
    {
        // Set success border
        textBox.BorderBrush = Brushes.Green;
        textBox.BorderThickness = new Thickness(2);

        // Set success tooltip
        textBox.ToolTip = $"✅ {successMessage}\n\nPress Escape to reset to original value.";

        // Add background tint to make success more visible
        textBox.Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0));
    }

    /// <summary>
    /// Updates integer display in a TextBox based on hex/decimal preference
    /// </summary>
    public static void UpdateIntegerDisplay(TextBox textBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        if (propertyItem.Value == null)
        {
            textBox.Text = string.Empty;
            return;
        }

        try
        {
            var longValue = System.Convert.ToInt64(propertyItem.Value);
            textBox.Text = isHexadecimal ? $"0x{longValue:X}" : longValue.ToString();
        }
        catch
        {
            textBox.Text = propertyItem.Value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Updates property value from integer text input with validation
    /// </summary>
    public static void UpdatePropertyFromIntegerText(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        var text = textBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            try
            {
                propertyItem.Value = null;
                ClearIntegerValidationError(textBox);
            }
            catch (Exception ex)
            {
                ShowIntegerValidationError(textBox, ex.Message);
            }
            return;
        }

        try
        {
            long longValue;

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = text.Substring(2);
                longValue = System.Convert.ToInt64(hexValue, 16);
            }
            else
            {
                longValue = System.Convert.ToInt64(text);
            }

            var targetType = propertyItem.PropertyType;
            object convertedValue;

            if (targetType == typeof(int) || targetType == typeof(int?))
                convertedValue = (int)longValue;
            else if (targetType == typeof(uint) || targetType == typeof(uint?))
                convertedValue = (uint)longValue;
            else if (targetType == typeof(long) || targetType == typeof(long?))
                convertedValue = longValue;
            else if (targetType == typeof(ulong) || targetType == typeof(ulong?))
                convertedValue = (ulong)longValue;
            else if (targetType == typeof(short) || targetType == typeof(short?))
                convertedValue = (short)longValue;
            else if (targetType == typeof(ushort) || targetType == typeof(ushort?))
                convertedValue = (ushort)longValue;
            else if (targetType == typeof(byte) || targetType == typeof(byte?))
                convertedValue = (byte)longValue;
            else if (targetType == typeof(sbyte) || targetType == typeof(sbyte?))
                convertedValue = (sbyte)longValue;
            else
                convertedValue = longValue;

            try
            {
                // Store the original value for comparison
                var originalValue = propertyItem.Value;

                propertyItem.Value = convertedValue;
                UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);

                // Check if the value was actually changed
                bool valueChanged = !AreValuesEqual(originalValue, convertedValue);

                if (valueChanged)
                {
                    // Show success state for modified values
                    ShowIntegerValidationSuccess(textBox);
                }
                else
                {
                    // Clear any previous styling if value unchanged
                    ClearIntegerValidationError(textBox);
                }
            }
            catch (Exception setValueEx)
            {
                ShowIntegerValidationError(textBox, $"Failed to set value: {setValueEx.Message}");
                UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
            }
        }
        catch (Exception parseEx)
        {
            ShowIntegerValidationError(textBox, $"Invalid number format: {parseEx.Message}");
            UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
        }
    }

    /// <summary>
    /// Adds validation behavior to a TextBox for property editing
    /// </summary>
    private static void AddValidationBehavior(TextBox textBox, PropertyHierarchyItem propertyItem, Action<TextBox, PropertyHierarchyItem, Brush, object>? customValidation = null)
    {
        // Store original values for reset functionality
        var originalBorderBrush = textBox.BorderBrush;
        var originalToolTip = textBox.ToolTip;

        // Add TextChanged validation
        textBox.TextChanged += (sender, e) =>
        {
            if (sender is TextBox tb)
            {
                if (customValidation != null)
                {
                    // Use custom validation if provided
                    customValidation(tb, propertyItem, originalBorderBrush, originalToolTip);
                }
                else if (ShouldValidateOnTextChanged(propertyItem))
                {
                    // Use default validation for types that use TypeConverter
                    ValidateTextBoxValue(tb, propertyItem, originalBorderBrush, originalToolTip);
                }
            }
        };

        // Add key handler to reset on Escape
        textBox.KeyDown += (sender, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape && sender is TextBox tb)
            {
                // Reset to original value and clear error state
                tb.Text = propertyItem.FormattedValue ?? string.Empty;
                ClearValidationError(tb, originalBorderBrush, originalToolTip);
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// Compares two arrays for equality
    /// </summary>
    private static bool AreArraysEqual(Array? array1, Array? array2)
    {
        // Handle null cases
        if (array1 == null && array2 == null) return true;
        if (array1 == null || array2 == null) return false;

        // Check if lengths are different
        if (array1.Length != array2.Length) return false;

        // Compare each element
        for (int i = 0; i < array1.Length; i++)
        {
            var item1 = array1.GetValue(i);
            var item2 = array2.GetValue(i);

            if (!AreValuesEqual(item1, item2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two valua
    /// <summary>
    /// Determines if a pi
    /// <summary>
    /// Determines if a property should validate on TextChanged (vs binding validation)
    /// </summary>

    /// <summary>
    /// Compares two values for equality, handling nulls appropriately
    /// </summary>

    // ===== PRIVATE HELPER METHODS =====

    private static bool AreValuesEqual(object? value1, object? value2)
    {
        // Handle null cases
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        // For numeric types, ensure we're comparing the same type
        if (IsNumericType(value1.GetType()) && IsNumericType(value2.GetType()))
        {
            try
            {
                // Convert both to decimal for comparison to handle different numeric types
                var decimal1 = Convert.ToDecimal(value1);
                var decimal2 = Convert.ToDecimal(value2);
                return decimal1 == decimal2;
            }
            catch
            {
                // Fall back to standard comparison if conversion fails
                return value1.Equals(value2);
            }
        }

        // For other types, use standard equality comparison
        return value1.Equals(value2);
    }

    /// <summary>
    /// Checks if a type is numeric
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    /// <summary>
    /// Sets up binding for integer editor components
    /// </summary>
    private static void SetupIntegerEditorBinding(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        checkBox.Checked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, true);
        checkBox.Unchecked += (s, e) => UpdateIntegerDisplay(textBox, propertyItem, false);

        textBox.LostFocus += (s, e) => UpdatePropertyFromIntegerText(textBox, checkBox, propertyItem);

        UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
    }

    private static bool ShouldValidateOnTextChanged(PropertyHierarchyItem propertyItem)
    {
        // Apply validation to ALL types that use TextBox editors
        // This ensures consistent validation behavior across all property types
        var type = propertyItem.PropertyType;

        // Skip validation for non-TextBox types
        return !type.IsEnum && !type.IsArray && type != typeof(bool) && type != typeof(bool?);
    }

    /// <summary>
    /// Validates the TextBox value using TypeConverter
    /// </summary>
    private static void ValidateTextBoxValue(TextBox textBox, PropertyHierarchyItem propertyItem, Brush originalBorderBrush, object originalToolTip)
    {
        try
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyItem.PropertyType);

            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    // Store the original value for comparison
                    var originalValue = propertyItem.Value;

                    // Test conversion - if it succeeds, update the property
                    var convertedValue = converter.ConvertFromString(textBox.Text);

                    // Try to set the value, but catch any WMI-level errors too
                    try
                    {
                        propertyItem.Value = convertedValue;

                        // Check if the value was actually changed
                        bool valueChanged = !AreValuesEqual(originalValue, convertedValue);

                        if (valueChanged)
                        {
                            // Show success state for modified values
                            ShowValidationSuccess(textBox);
                        }
                        else
                        {
                            // Clear any previous styling if value unchanged
                            ClearValidationError(textBox, originalBorderBrush, originalToolTip);
                        }
                    }
                    catch (Exception setValueEx)
                    {
                        // If the conversion succeeded but setting the value failed (e.g., WMI type mismatch)
                        ShowValidationError(textBox, $"Value conversion succeeded but assignment failed: {setValueEx.Message}");
                    }
                }
                catch (Exception conversionEx)
                {
                    ShowValidationError(textBox, conversionEx.Message);
                }
            }
            else
            {
                ShowValidationError(textBox, "Cannot convert this value to the required type.");
            }
        }
        catch (Exception ex)
        {
            // Show visual feedback for validation error
            ShowValidationError(textBox, ex.Message);
        }
    }
}