using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Converters;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for char properties providing text-based editing with hex/decimal support and validation.
/// </summary>
public static class CharEditor
{
    /// <summary>
    /// Creates a complete char editor with hex/decimal support
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

        var textBox = UIHelpers.CreateStandardTextBox(null, "Enter character (or 0xHEX)", propertyItem, new Thickness(0), CustomCharValidation);

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
            SetupCharEditorBinding(textBox, checkBox, propertyItem);
        }

        return grid;
    }

    // New-style validation delegate
    private static ValidationManager.ValidationResult CustomCharValidation(string text, object? originalValue)
    {
        if (string.IsNullOrEmpty(text))
            return ValidationManager.ValidationResult.Valid(null, !ValidationManager.AreValuesEqual(null, originalValue));
        try
        {
            object convertedValue;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = text.Substring(2);
                convertedValue = ParseHexValueForChar(hexValue);
            }
            else if (text.Length == 1)
            {
                convertedValue = text[0];
            }
            else
            {
                throw new FormatException("Input must be a single character or a valid hex code.");
            }
            bool isModified = !ValidationManager.AreValuesEqual(convertedValue, originalValue);
            return ValidationManager.ValidationResult.Valid(convertedValue, isModified);
        }
        catch (Exception ex)
        {
            return ValidationManager.ValidationResult.Error($"Invalid char format: {ex.Message}");
        }
    }

    private static string FormatCharAsHex(object value)
    {
        if (value == null) return string.Empty;
        try
        {
            char c = Convert.ToChar(value);
            return $"0x{((ushort)c):X4}";
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static object ParseHexValueForChar(string hexValue)
    {
        ushort code = Convert.ToUInt16(hexValue, 16);
        return Convert.ToChar(code);
    }

    private static void SetupCharEditorBinding(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        checkBox.Checked += (s, e) => UpdateCharDisplayWithValidationPreservation(textBox, checkBox, propertyItem, true);
        checkBox.Unchecked += (s, e) => UpdateCharDisplayWithValidationPreservation(textBox, checkBox, propertyItem, false);
        // It's okay to have a second TextChanged handler here: ValidationManager handles validation/assignment/styling,
        // while this handler is only for enabling/disabling the hex checkbox based on validity.
        textBox.TextChanged += (s, e) =>
        {
            // Ensure propertyItem.Value is not null; pass string.Empty if it is
            var originalValue = propertyItem.Value;
            var result = CustomCharValidation(textBox.Text, originalValue);
            checkBox.IsEnabled = result.IsValid;
        };
        UpdateCharDisplay(textBox, propertyItem, checkBox.IsChecked == true);
    }

    private static bool ShouldDefaultToHex(object? value)
    {
        if (value == null) return false;
        try
        {
            char c = Convert.ToChar(value);
            return c > 127;
        }
        catch
        {
            return false;
        }
    }

    private static void UpdateCharDisplay(TextBox textBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        if (propertyItem.Value == null)
        {
            CaretPositionHelper.SetTextPreservingCaret(textBox, string.Empty);
            return;
        }
        try
        {
            var newText = isHexadecimal ? FormatCharAsHex(propertyItem.Value) : propertyItem.Value.ToString();
            if (textBox.Text != newText)
            {
                CaretPositionHelper.SetTextPreservingCaret(textBox, newText ?? string.Empty);
            }
        }
        catch
        {
            var newText = propertyItem.Value.ToString();
            if (textBox.Text != (newText ?? string.Empty))
            {
                CaretPositionHelper.SetTextPreservingCaret(textBox, newText ?? string.Empty);
            }
        }
    }

    private static void UpdateCharDisplayWithValidationPreservation(TextBox textBox, CheckBox checkBox, PropertyHierarchyItem propertyItem, bool isHexadecimal)
    {
        UpdateCharDisplay(textBox, propertyItem, isHexadecimal);
        ValidationManager.ApplyValidationStyling(textBox, propertyItem);
    }
}