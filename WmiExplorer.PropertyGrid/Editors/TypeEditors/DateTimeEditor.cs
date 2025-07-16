using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for DateTime properties providing text-based editing with validation.
/// </summary>
public static class DateTimeEditor
{
    /// <summary>
    /// Creates a standardized TextBox for DateTime property editing
    /// </summary>
    public static TextBox Create(PropertyHierarchyItem propertyItem)
    {
        // Use the formatted value as the initial text, and provide a placeholder
        var textBox = PropertyEditorUtils.CreateStandardTextBox(
            propertyItem.FormattedValue,
            "Enter date and time (e.g., 2024-06-01 13:45:00)",
            propertyItem,
            null,
            (text, originalValue) => CustomDateTimeValidation(text, originalValue)
        );
        return textBox;
    }

    private static ValidationManager.ValidationResult CustomDateTimeValidation(string? text, object? originalValue)
    {
        if (DateTime.TryParse(text, out var parsed))
        {
            DateTime? originalDateTime = null;
            if (originalValue is DateTime dt)
            {
                originalDateTime = dt;
            }
            else if (originalValue is string s && DateTime.TryParse(s, out var dtParsed))
            {
                originalDateTime = dtParsed;
            }
            // If originalValue is not parseable, treat as modified
            bool isModified = true;
            if (originalDateTime.HasValue)
            {
                isModified = !parsed.Equals(originalDateTime.Value);
            }
            return ValidationManager.ValidationResult.Valid(parsed, isModified);
        }
        return ValidationManager.ValidationResult.Error("Invalid date/time format");
    }
}