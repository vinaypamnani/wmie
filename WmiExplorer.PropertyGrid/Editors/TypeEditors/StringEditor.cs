using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for string properties providing text editing.
/// </summary>
public static class StringEditor
{
    /// <summary>
    /// Creates a standardized TextBox for string property editing
    /// </summary>
    public static TextBox Create(PropertyHierarchyItem propertyItem)
    {
        // MaxWidth constraint is applied within CreateStandardTextBox for the TextBox
        var textBox = UIHelpers.CreateStandardTextBox(propertyItem.FormattedValue, "Enter text", propertyItem);
        return textBox;
    }
}