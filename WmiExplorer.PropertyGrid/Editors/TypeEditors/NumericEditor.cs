using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for numeric properties (decimal, float, double) providing text-based editing with validation.
/// </summary>
public static class NumericEditor
{
    /// <summary>
    /// Creates a standardized TextBox for decimal/floating-point property editing
    /// </summary>
    public static TextBox Create(PropertyHierarchyItem propertyItem, Type propertyType)
    {
        var placeholderText = propertyType == typeof(decimal) ? "Enter decimal number (e.g., 123.45)" : "Enter decimal number";
        // Use custom validation for float/double to enforce precision
        if (propertyType == typeof(float) || propertyType == typeof(double))
        {
            var textBox = UIHelpers.CreateStandardTextBox(
                propertyItem.FormattedValue,
                placeholderText,
                propertyItem,
                null,
                (text, originalValue) => CustomFloatDoubleValidation(text, originalValue, propertyType)
            );
            return textBox;
        }
        // Default for decimal and others
        var defaultTextBox = UIHelpers.CreateStandardTextBox(propertyItem.FormattedValue, placeholderText, propertyItem);
        return defaultTextBox;
    }

    // Custom validation for float/double (Real32/Real64)
    public static ValidationManager.ValidationResult CustomFloatDoubleValidation(string text, object? originalValue, Type propertyType)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ValidationManager.ValidationResult.Valid(null, !ValidationManager.AreValuesEqual(null, originalValue));
        try
        {
            object convertedValue;
            if (propertyType == typeof(float))
            {
                if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out float parsed))
                    return ValidationManager.ValidationResult.Error($"Invalid number format for Real32 (float).");
                convertedValue = parsed;
                // Check for precision loss
                string roundTrip = parsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (!IsFloatInputEquivalent(text, roundTrip, propertyType))
                    return ValidationManager.ValidationResult.Error($"Value '{text}' cannot be exactly represented as a Real32 (float). Maximum 7 significant digits allowed.");
            }
            else if (propertyType == typeof(double))
            {
                if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double parsed))
                    return ValidationManager.ValidationResult.Error($"Invalid number format for Real64 (double).");
                convertedValue = parsed;
                // Check for precision loss
                string roundTrip = parsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (!IsFloatInputEquivalent(text, roundTrip, propertyType))
                    return ValidationManager.ValidationResult.Error($"Value '{text}' cannot be exactly represented as a Real64 (double). Maximum 15-16 significant digits allowed.");
            }
            else
            {
                return ValidationManager.ValidationResult.Error("Unsupported type for float/double validation.");
            }
            bool isModified = !ValidationManager.AreValuesEqual(convertedValue, originalValue);
            return ValidationManager.ValidationResult.Valid(convertedValue, isModified);
        }
        catch (Exception ex)
        {
            return ValidationManager.ValidationResult.Error($"Invalid number format: {ex.Message}");
        }
    }

    // Helper to compare input string to round-tripped value, ignoring insignificant formatting
    private static bool IsFloatInputEquivalent(string input, string roundTrip, Type propertyType)
    {
        // Normalize input: remove leading/trailing whitespace, lowercase, remove trailing zeros after decimal, remove leading plus
        string normInput = NormalizeFloatString(input);
        string normRoundTrip = NormalizeFloatString(roundTrip);
        // Accept both "." and "," as decimal separators for input
        if (System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",")
        {
            normInput = normInput.Replace(',', '.');
        }
        // Accept scientific notation equivalence
        return normInput.Equals(normRoundTrip, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string NormalizeFloatString(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.StartsWith("+")) s = s.Substring(1);
        // Replace comma with dot for decimal separator
        s = s.Replace(',', '.');
        // Remove trailing zeros after decimal point
        if (s.Contains("."))
        {
            s = s.TrimEnd('0');
            if (s.EndsWith(".")) s = s.TrimEnd('.');
        }
        // Remove leading zeros (except for numbers like 0.x)
        if (s.StartsWith("0") && s.Length > 1 && s[1] != '.')
        {
            s = s.TrimStart('0');
        }
        return s;
    }
}