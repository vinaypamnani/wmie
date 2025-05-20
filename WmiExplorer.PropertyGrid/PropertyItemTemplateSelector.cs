using System.Windows;
using System.Windows.Controls;

namespace WmiExplorer.PropertyGrid
{
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
                    if (PropertyItemTemplate != null)
                    {
                        return PropertyItemTemplate;
                    }
                    // Fall back to resource lookup based on type
                    if (hierarchyItem.IsCategory)
                    {
                        return element.TryFindResource("CategoryItemTemplate") as DataTemplate;
                    }
                    return element.TryFindResource("PropertyItemTemplate") as DataTemplate;
                }
            }
            return null;
        }
    }
}