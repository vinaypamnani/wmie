using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Converters;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for integer properties providing text-based editing with hex/decimal support and validation.
/// </summary>
public static class IntegerEditor
{
    /// <summary>
    /// Creates a complete integer editor with hex/decimal support
    /// </summary>
    public static Grid Create(PropertyHierarchyItem propertyItem)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        UIHelpers.ApplyMaxWidthConstraint(grid);

        var textBox = UIHelpers.CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem, new Thickness(0), CustomIntegerValidation);

        var checkBox = new CheckBox
        {
            Content = "Hex",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = EditorInfrastructure.CHECKBOX_HEX_MARGIN,
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
    /// Converts a long value to the appropriate target integer type
    /// </summary>
    private static object ConvertToTargetIntegerType(long value, Type targetType)
    {
        return targetType switch
        {
            var t when t == typeof(int) || t == typeof(int?) => (int)value,
            var t when t == typeof(uint) || t == typeof(uint?) => (uint)value,
            var t when t == typeof(long) || t == typeof(long?) => value,
            var t when t == typeof(ulong) || t == typeof(ulong?) => (ulong)value,
            var t when t == typeof(short) || t == typeof(short?) => (short)value,
            var t when t == typeof(ushort) || t == typeof(ushort?) => (ushort)value,
            var t when t == typeof(byte) || t == typeof(byte?) => (byte)value,
            var t when t == typeof(sbyte) || t == typeof(sbyte?) => (sbyte)value,
            _ => value
        };
    }

    // New-style validation delegate
    private static ValidationManager.ValidationResult CustomIntegerValidation(string text, object originalValue)
    {
        if (string.IsNullOrEmpty(text))
            return ValidationManager.ValidationResult.Valid(null, !ValidationManager.AreValuesEqual(null, originalValue));
        try
        {
            object convertedValue;
            var targetType = originalValue?.GetType() ?? typeof(int); // Fallback to int if originalValue is null
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = text.Substring(2);
                convertedValue = ParseHexValueForType(hexValue, targetType);
            }
            else
            {
                var longValue = System.Convert.ToInt64(text);
                convertedValue = ConvertToTargetIntegerType(longValue, targetType);
            }
            bool isModified = !ValidationManager.AreValuesEqual(convertedValue, originalValue);
            return ValidationManager.ValidationResult.Valid(convertedValue, isModified);
        }
        catch (Exception ex)
        {
            return ValidationManager.ValidationResult.Error($"Invalid number format: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats an integer value as hexadecimal with appropriate digit count for the type
    /// </summary>
    private static string FormatIntegerAsHex(object value, Type propertyType)
    {
        if (value == null) return string.Empty;

        try
        {
            // Convert to the specific type to get proper two's complement representation
            return propertyType switch
            {
                var t when t == typeof(byte) || t == typeof(byte?) =>
                    $"0x{Convert.ToByte(value):X2}",
                var t when t == typeof(sbyte) || t == typeof(sbyte?) =>
                    $"0x{(byte)Convert.ToSByte(value):X2}",
                var t when t == typeof(ushort) || t == typeof(ushort?) =>
                    $"0x{Convert.ToUInt16(value):X4}",
                var t when t == typeof(short) || t == typeof(short?) =>
                    $"0x{(ushort)Convert.ToInt16(value):X4}",
                var t when t == typeof(uint) || t == typeof(uint?) =>
                    $"0x{Convert.ToUInt32(value):X8}",
                var t when t == typeof(int) || t == typeof(int?) =>
                    $"0x{(uint)Convert.ToInt32(value):X8}",
                var t when t == typeof(ulong) || t == typeof(ulong?) =>
                    $"0x{Convert.ToUInt64(value):X16}",
                var t when t == typeof(long) || t == typeof(long?) =>
                    $"0x{(ulong)Convert.ToInt64(value):X16}",
                _ => $"0x{Convert.ToInt64(value):X}"
            };
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Parses a hex string for a specific integer type, handling two's complement properly
    /// </summary>
    private static object ParseHexValueForType(string hexValue, Type targetType)
    {
        // For signed types, we need to handle two's complement representation correctly
        return targetType switch
        {
            var t when t == typeof(byte) || t == typeof(byte?) =>
                Convert.ToByte(hexValue, 16),
            var t when t == typeof(sbyte) || t == typeof(sbyte?) =>
                (sbyte)Convert.ToByte(hexValue, 16), // Parse as byte, then cast to sbyte for two's complement
            var t when t == typeof(ushort) || t == typeof(ushort?) =>
                Convert.ToUInt16(hexValue, 16),
            var t when t == typeof(short) || t == typeof(short?) =>
                (short)Convert.ToUInt16(hexValue, 16), // Parse as ushort, then cast to short for two's complement
            var t when t == typeof(uint) || t == typeof(uint?) =>
                Convert.ToUInt32(hexValue, 16),
            var t when t == typeof(int) || t == typeof(int?) =>
                (int)Convert.ToUInt32(hexValue, 16), // Parse as uint, then cast to int for two's complement
            var t when t == typeof(ulong) || t == typeof(ulong?) =>
                Convert.ToUInt64(hexValue, 16),
            var t when t == typeof(long) || t == typeof(long?) =>
                (long)Convert.ToUInt64(hexValue, 16), // Parse as ulong, then cast to long for two's complement
            _ => Convert.ToInt64(hexValue, 16)
        };
    }

    /// <summary>
    /// Sets up binding for integer editor components
    /// </summary>
    private static void SetupIntegerEditorBinding(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        checkBox.Checked += (s, e) => UpdateIntegerDisplayWithValidationPreservation(textBox, checkBox, propertyItem, true);
        checkBox.Unchecked += (s, e) => UpdateIntegerDisplayWithValidationPreservation(textBox, checkBox, propertyItem, false);
        // It's okay to have a second TextChanged handler here: ValidationManager handles validation/assignment/styling,
        // while this handler is only for enabling/disabling the hex checkbox based on validity.
        textBox.TextChanged += (s, e) =>
        {
            // Ensure propertyItem.Value is not null; pass string.Empty if it is
            var originalValue = propertyItem.Value ?? string.Empty;
            var result = CustomIntegerValidation(textBox.Text, originalValue);
            checkBox.IsEnabled = result.IsValid;
        };
        UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
    }

    /// <summary>
    /// Determines if a value should default to hexadecimal display
    /// </summary>
    private static bool ShouldDefaultToHex(object? value)
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
    /// Updates integer display in a TextBox based on hex/decimal preference
    /// </summary>
    private static void UpdateIntegerDisplay(TextBox textBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        if (propertyItem.Value == null)
        {
            CaretPositionHelper.SetTextPreservingCaret(textBox, string.Empty);
            return;
        }

        try
        {
            var newText = isHexadecimal ?
                FormatIntegerAsHex(propertyItem.Value, propertyItem.PropertyType) :
                (propertyItem.Value.ToString() ?? string.Empty);

            // Only update text if it's actually different to avoid unnecessary caret movement
            if (textBox.Text != newText)
            {
                CaretPositionHelper.SetTextPreservingCaret(textBox, newText);
            }
        }
        catch
        {
            var newText = propertyItem.Value.ToString() ?? string.Empty;
            if (textBox.Text != newText)
            {
                CaretPositionHelper.SetTextPreservingCaret(textBox, newText);
            }
        }
    }

    /// <summary>
    /// Updates integer display while preserving validation state (used for hex/decimal toggle)
    /// </summary>
    private static void UpdateIntegerDisplayWithValidationPreservation(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        // Update the display
        UpdateIntegerDisplay(textBox, propertyItem, isHexadecimal);

        // Apply appropriate validation styling using the centralized ValidationManager
        ValidationManager.ApplyValidationStyling(textBox, propertyItem);
    }
}