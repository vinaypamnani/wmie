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
        var textBox = UIHelpers.CreateStandardTextBox(null, "Enter text", propertyItem);

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem, UpdateSourceTrigger.LostFocus);
        textBox.SetBinding(TextBox.TextProperty, binding);

        return textBox;
    }
}