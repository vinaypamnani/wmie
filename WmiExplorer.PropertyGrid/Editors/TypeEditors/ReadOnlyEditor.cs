using System.Windows;
using System.Windows.Controls;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.TypeEditors;

/// <summary>
/// Specialized editor for read-only properties providing text display.
/// </summary>
public static class ReadOnlyEditor
{
    /// <summary>
    /// Creates a read-only TextBlock for displaying property values
    /// </summary>
    public static TextBlock Create(PropertyHierarchyItem propertyItem)
    {
        return new TextBlock
        {
            Text = propertyItem.FormattedValue ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD
        };
    }
}