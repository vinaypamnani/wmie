using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for enum properties providing ComboBox editing.
/// </summary>
public static class EnumEditor
{
    /// <summary>
    /// Creates a standardized ComboBox for enum property editing
    /// </summary>
    public static ComboBox Create(PropertyHierarchyItem propertyItem, Type enumType)
    {
        var comboBox = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD,
            ItemsSource = Enum.GetValues(enumType),
            MinWidth = 100
        };

        // MaxWidth constraint is applied only to the ComboBox for best practice
        UIHelpers.ApplyMaxWidthConstraint(comboBox);

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem);
        comboBox.SetBinding(ComboBox.SelectedItemProperty, binding);
        EditorInfrastructure.AttachSelectOnFocus(comboBox, propertyItem);

        return comboBox;
    }
}