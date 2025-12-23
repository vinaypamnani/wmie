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

        PropertyEditorUtils.InitializeEditor(checkBox, propertyItem);

        var binding = EditorInfrastructure.CreateStandardPropertyBinding(propertyItem);
        checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
        EditorInfrastructure.AttachSelectOnFocus(checkBox, propertyItem);

        // Validation/modified tracking
        checkBox.Checked += (s, e) => ApplyValidation(checkBox, propertyItem);
        checkBox.Unchecked += (s, e) => ApplyValidation(checkBox, propertyItem);
        // Also apply on load
        checkBox.Loaded += (s, e) => ApplyValidation(checkBox, propertyItem);

        return checkBox;
    }

    private static void ApplyValidation(CheckBox checkBox, PropertyHierarchyItem propertyItem)
    {
        var current = checkBox.IsChecked;
        var original = propertyItem.OriginalValue as bool?;
        if (current != original)
        {
            ValidationManager.SetValidationModified(checkBox);
        }
        else
        {
            ValidationManager.SetValidationNormal(checkBox);
        }
    }
}