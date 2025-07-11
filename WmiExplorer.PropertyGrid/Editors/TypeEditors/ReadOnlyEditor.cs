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
    public static TextBox Create(PropertyHierarchyItem propertyItem)
    {
        return new TextBox
        {
            Text = propertyItem.FormattedValue ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            TextWrapping = TextWrapping.Wrap,
            ContextMenu = Application.Current.FindResource("PropertyGridTextBoxContextMenu") as ContextMenu ?? null,
            Margin = EditorInfrastructure.CONTROL_MARGIN_STANDARD
        };
    }
}