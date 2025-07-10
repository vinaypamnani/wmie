using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for boolean properties providing checkbox editing.
/// </summary>
public static class BooleanEditor
{
    /// <summary>
    /// Creates a standardized CheckBox for boolean property editing
    /// </summary>
    public static CheckBox Create(PropertyHierarchyItem propertyItem)
    {
        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD
        };

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem);
        checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
        EditorInfrastructure.AttachSelectOnFocus(checkBox, propertyItem);

        return checkBox;
    }
}