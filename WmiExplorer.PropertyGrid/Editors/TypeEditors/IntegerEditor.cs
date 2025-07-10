using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        var textBox = UIHelpers.CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem, new Thickness(0));

        // Replace the default validation with custom integer validation
        ValidationManager.AddValidationBehavior(textBox, propertyItem, CustomIntegerValidation);

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
    /// Clears integer validation error state from a TextBox
    /// </summary>
    private static void ClearIntegerValidationError(TextBox textBox)
    {
        ValidationManager.SetValidationNormal(textBox);

        // Re-enable hex checkbox since error state is cleared
        var hexCheckBox = FindAssociatedHexCheckBox(textBox);
        if (hexCheckBox != null)
        {
            hexCheckBox.IsEnabled = true;
        }
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

    /// <summary>
    /// Custom validation for integer editors with hex checkbox support
    /// </summary>
    private static void CustomIntegerValidation(TextBox textBox, PropertyHierarchyItem propertyItem, System.Windows.Media.Brush originalBorderBrush, object originalToolTip)
    {
        // This implements the integer-specific validation logic while integrating with hex checkbox
        // The actual validation logic is handled in the UpdatePropertyFromIntegerText method
    }

    /// <summary>
    /// Finds the hex CheckBox associated with an integer TextBox by traversing the visual tree
    /// </summary>
    private static CheckBox? FindAssociatedHexCheckBox(TextBox textBox)
    {
        try
        {
            // The TextBox should be in column 0 of a Grid, with the CheckBox in column 1
            if (textBox.Parent is Grid parentGrid)
            {
                foreach (UIElement child in parentGrid.Children)
                {
                    if (child is CheckBox checkBox && Grid.GetColumn(checkBox) == 1)
                    {
                        // Additional check to ensure this is the hex checkbox
                        if (checkBox.Content?.ToString()?.Equals("Hex", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return checkBox;
                        }
                    }
                }
            }
        }
        catch
        {
            // If anything goes wrong with visual tree traversal, just return null
        }

        return null;
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

        textBox.LostFocus += (s, e) => UpdatePropertyFromIntegerText(textBox, checkBox, propertyItem);

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
    /// Shows integer validation error state on a TextBox
    /// </summary>
    private static void ShowIntegerValidationError(TextBox textBox, string errorMessage)
    {
        ValidationManager.SetValidationError(textBox, errorMessage);

        // Disable hex checkbox to prevent confusing behavior with invalid values
        var hexCheckBox = FindAssociatedHexCheckBox(textBox);
        if (hexCheckBox != null)
        {
            hexCheckBox.IsEnabled = false;
        }
    }

    /// <summary>
    /// Shows integer validation success state on a TextBox for modified values
    /// </summary>
    private static void ShowIntegerValidationSuccess(TextBox textBox, string successMessage = "Value modified")
    {
        ValidationManager.SetValidationModified(textBox, successMessage);

        // Re-enable hex checkbox since validation succeeded
        var hexCheckBox = FindAssociatedHexCheckBox(textBox);
        if (hexCheckBox != null)
        {
            hexCheckBox.IsEnabled = true;
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
    /// Updates property value from integer text input with validation
    /// </summary>
    private static void UpdatePropertyFromIntegerText(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
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
            var targetType = propertyItem.PropertyType;
            object convertedValue;

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

            try
            {
                // Store the original value for comparison
                var originalValue = propertyItem.Value;

                propertyItem.Value = convertedValue;

                // Only update display if format needs normalization (avoid unnecessary text changes)
                var isHexadecimal = checkBox.IsChecked == true;
                var expectedText = isHexadecimal ?
                    FormatIntegerAsHex(convertedValue, targetType) :
                    convertedValue.ToString();
                if (textBox.Text != expectedText)
                {
                    // Format needs correction - update while preserving caret position
                    CaretPositionHelper.PreserveCaretPosition(textBox, () =>
                    {
                        UpdateIntegerDisplay(textBox, propertyItem, checkBox.IsChecked == true);
                    });
                }

                // Check if the value was actually changed
                bool valueChanged = !ValidationManager.AreValuesEqual(originalValue, convertedValue);

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
                // Leave the user's input as-is so they can see what's wrong and fix it
            }
        }
        catch (Exception parseEx)
        {
            ShowIntegerValidationError(textBox, $"Invalid number format: {parseEx.Message}");
            // Leave the user's input as-is so they can see what's wrong and fix it
        }
    }
}