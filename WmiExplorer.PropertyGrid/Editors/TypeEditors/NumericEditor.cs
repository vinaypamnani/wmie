using System.Windows.Controls;
using System.Windows.Data;
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
        var textBox = UIHelpers.CreateStandardTextBox(null, placeholderText, propertyItem);

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem, UpdateSourceTrigger.LostFocus);
        textBox.SetBinding(TextBox.TextProperty, binding);

        return textBox;
    }
}