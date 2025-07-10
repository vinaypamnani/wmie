using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for DateTime properties providing date picker editing.
/// </summary>
public static class DateTimeEditor
{
    /// <summary>
    /// Creates a standardized DatePicker for DateTime property editing
    /// </summary>
    public static DatePicker Create(PropertyHierarchyItem propertyItem)
    {
        var datePicker = new DatePicker
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD,
            MinWidth = 180
        };

        UIHelpers.ApplyMaxWidthConstraint(datePicker);

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem);
        datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);
        EditorInfrastructure.AttachSelectOnFocus(datePicker, propertyItem);

        return datePicker;
    }
}