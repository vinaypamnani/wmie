using System.Numerics;
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
    #region methods

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

        var textBox = UIHelpers.CreateStandardTextBox(null, "Enter number (decimal or 0xHEX)", propertyItem, new Thickness(0), CustomIntegerValidation);
        // MaxWidth constraint is applied within CreateStandardTextBox

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

    // New-style validation delegate
    public static ValidationManager.ValidationResult CustomIntegerValidation(string text, object? originalValue)
    {
        if (string.IsNullOrEmpty(text))
            return ValidationManager.ValidationResult.Valid(null, !ValidationManager.AreValuesEqual(null, originalValue));
        try
        {
            object convertedValue;
            var targetType = originalValue?.GetType() ?? typeof(int); // Fallback to int if originalValue is null
            bool isHex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string normalizedInput = isHex ? text.Substring(2).TrimStart('0').ToUpperInvariant() : text.TrimStart('0');
            if (string.IsNullOrEmpty(normalizedInput)) normalizedInput = "0";

            BigInteger bigValue;
            if (isHex)
            {
                bigValue = BigInteger.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
                // Apply unsigned fix for negative values (same as ParseHexValueForType)
                if (bigValue.Sign < 0)
                {
                    if (targetType == typeof(byte) || targetType == typeof(byte?))
                        bigValue += BigInteger.One << 8;
                    else if (targetType == typeof(ushort) || targetType == typeof(ushort?))
                        bigValue += BigInteger.One << 16;
                    else if (targetType == typeof(uint) || targetType == typeof(uint?))
                        bigValue += BigInteger.One << 32;
                    else if (targetType == typeof(ulong) || targetType == typeof(ulong?))
                        bigValue += BigInteger.One << 64;
                }
            }
            else
                bigValue = BigInteger.Parse(text, System.Globalization.NumberStyles.Integer);

            convertedValue = ConvertToTargetIntegerType(bigValue, targetType);

            // Convert back to string for comparison
            string resultString = isHex
                ? FormatIntegerAsHex(convertedValue, targetType).Substring(2).TrimStart('0').ToUpperInvariant()
                : convertedValue?.ToString() ?? "";
            if (string.IsNullOrEmpty(resultString)) resultString = "0";

            // For decimal, handle negative zero edge case
            if (!isHex && (normalizedInput == "-0" || resultString == "-0"))
            {
                normalizedInput = "0";
                resultString = "0";
            }

            if (normalizedInput != resultString)
            {
                return ValidationManager.ValidationResult.Error($"Value '{text}' is out of range for type {targetType.Name}.");
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
    /// Converts a long value to the appropriate target integer type
    /// </summary>
    private static object ConvertToTargetIntegerType(BigInteger value, Type targetType)
    {
        // Console.WriteLine debug logging removed for production
        if (targetType == typeof(byte) || targetType == typeof(byte?))
        {
            if (value >= new BigInteger(byte.MinValue) && value <= new BigInteger(byte.MaxValue))
            {
                try
                {
                    var castValue = (byte)value;
                    return castValue;
                }
                catch (Exception)
                {
                    var fallback = Convert.ToByte((int)value);
                    return fallback;
                }
            }
            throw new OverflowException($"Value is out of range for byte. Valid range: {byte.MinValue} to {byte.MaxValue}.");
        }
        if (targetType == typeof(sbyte) || targetType == typeof(sbyte?))
        {
            if (value >= new BigInteger(sbyte.MinValue) && value <= new BigInteger(sbyte.MaxValue))
            {
                var castValue = (sbyte)value;
                return castValue;
            }
            throw new OverflowException($"Value is out of range for sbyte. Valid range: {sbyte.MinValue} to {sbyte.MaxValue}.");
        }
        if (targetType == typeof(short) || targetType == typeof(short?))
        {
            if (value >= new BigInteger(short.MinValue) && value <= new BigInteger(short.MaxValue))
            {
                var castValue = (short)value;
                return castValue;
            }
            throw new OverflowException($"Value is out of range for short. Valid range: {short.MinValue} to {short.MaxValue}.");
        }
        if (targetType == typeof(ushort) || targetType == typeof(ushort?))
        {
            if (value >= new BigInteger(ushort.MinValue) && value <= new BigInteger(ushort.MaxValue))
            {
                try
                {
                    var castValue = (ushort)value;
                    return castValue;
                }
                catch (Exception)
                {
                    var fallback = Convert.ToUInt16((int)value);
                    return fallback;
                }
            }
            throw new OverflowException($"Value is out of range for ushort. Valid range: {ushort.MinValue} to {ushort.MaxValue}.");
        }
        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            if (value >= new BigInteger(int.MinValue) && value <= new BigInteger(int.MaxValue))
            {
                var castValue = (int)value;
                return castValue;
            }
            throw new OverflowException($"Value is out of range for int. Valid range: {int.MinValue} to {int.MaxValue}.");
        }
        if (targetType == typeof(uint) || targetType == typeof(uint?))
        {
            if (value >= new BigInteger(uint.MinValue) && value <= new BigInteger(uint.MaxValue))
            {
                try
                {
                    var castValue = (uint)value;
                    return castValue;
                }
                catch (Exception)
                {
                    var fallback = Convert.ToUInt32((long)value);
                    return fallback;
                }
            }
            throw new OverflowException($"Value is out of range for uint. Valid range: {uint.MinValue} to {uint.MaxValue}.");
        }
        if (targetType == typeof(long) || targetType == typeof(long?))
        {
            if (value >= new BigInteger(long.MinValue) && value <= new BigInteger(long.MaxValue))
            {
                var castValue = (long)value;
                return castValue;
            }
            throw new OverflowException($"Value is out of range for long. Valid range: {long.MinValue} to {long.MaxValue}.");
        }
        if (targetType == typeof(ulong) || targetType == typeof(ulong?))
        {
            if (value >= new BigInteger(ulong.MinValue) && value <= new BigInteger(ulong.MaxValue))
            {
                try
                {
                    var castValue = (ulong)value;
                    return castValue;
                }
                catch (Exception)
                {
                    var fallback = Convert.ToUInt64((decimal)value);
                    return fallback;
                }
            }
            throw new OverflowException($"Value is out of range for ulong. Valid range: {ulong.MinValue} to {ulong.MaxValue}.");
        }
        if (value >= new BigInteger(long.MinValue) && value <= new BigInteger(long.MaxValue))
        {
            var castValue = (long)value;
            return castValue;
        }
        throw new OverflowException($"Value is out of range for long. Valid range: {long.MinValue} to {long.MaxValue}.");
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
        BigInteger bigValue = BigInteger.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        // Fix for unsigned types: if negative, add 2^(bit width)
        if (bigValue.Sign < 0)
        {
            if (targetType == typeof(byte) || targetType == typeof(byte?))
                bigValue += BigInteger.One << 8;
            else if (targetType == typeof(ushort) || targetType == typeof(ushort?))
                bigValue += BigInteger.One << 16;
            else if (targetType == typeof(uint) || targetType == typeof(uint?))
                bigValue += BigInteger.One << 32;
            else if (targetType == typeof(ulong) || targetType == typeof(ulong?))
                bigValue += BigInteger.One << 64;
        }
        return ConvertToTargetIntegerType(bigValue, targetType);
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

    #endregion 
}