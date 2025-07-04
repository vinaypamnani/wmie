using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Selects the appropriate template for property items based on their type,
/// supporting both traditional CustomPropertyItem and the new hierarchical PropertyHierarchyItem.
/// </summary>
public class PropertyItemTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Gets or sets the template for category items in the hierarchy.
    /// </summary>
    public DataTemplate? CategoryItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for property items when using card-style display.
    /// </summary>
    public DataTemplate? PropertyEditorCardTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for standard property items in the hierarchy.
    /// </summary>
    public DataTemplate? PropertyItemTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is FrameworkElement element && item != null)
        {
            // Only support PropertyHierarchyItem
            if (item is PropertyHierarchyItem hierarchyItem)
            {
                // Use explicitly provided templates if available
                if (hierarchyItem.IsCategory && CategoryItemTemplate != null)
                {
                    return CategoryItemTemplate;
                }

                // Check if we should use card style by looking for PropertyGrid ancestor
                bool useCardStyle = false;
                var ancestor = container;

                // First try visual tree traversal
                while (ancestor != null)
                {
                    if (ancestor is PropertyGrid propertyGrid)
                    {
                        useCardStyle = propertyGrid.UseCardStyleEditor;
                        break;
                    }
                    ancestor = VisualTreeHelper.GetParent(ancestor);
                }

                // If visual tree didn't work, try logical tree
                if (ancestor == null)
                {
                    ancestor = container;
                    while (ancestor != null)
                    {
                        if (ancestor is PropertyGrid propertyGrid)
                        {
                            useCardStyle = propertyGrid.UseCardStyleEditor;
                            break;
                        }
                        ancestor = LogicalTreeHelper.GetParent(ancestor);
                    }
                }

                // Select appropriate property template
                if (useCardStyle && PropertyEditorCardTemplate != null)
                {
                    return PropertyEditorCardTemplate;
                }
                if (PropertyItemTemplate != null)
                {
                    return PropertyItemTemplate;
                }

                // Fall back to resource lookup based on type
                if (hierarchyItem.IsCategory)
                {
                    return element.TryFindResource("CategoryItemTemplate") as DataTemplate;
                }

                // Try card template first if card style is enabled
                if (useCardStyle)
                {
                    var cardTemplate = element.TryFindResource("PropertyEditorCardTemplate") as DataTemplate;
                    if (cardTemplate != null)
                        return cardTemplate;
                }

                return element.TryFindResource("PropertyItemTemplate") as DataTemplate;
            }
        }
        return null;
    }
}