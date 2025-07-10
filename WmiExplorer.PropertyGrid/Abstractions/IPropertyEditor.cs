using System.Windows;

namespace WmiExplorer.PropertyGrid.Abstractions;

/// <summary>
/// Interface for property editors that can create appropriate UI controls for editing properties.
/// This allows for type-specific or domain-specific editor implementations.
/// </summary>
public interface IPropertyEditor
{
    /// <summary>
    /// Creates an editor UI element for the specified property item.
    /// </summary>
    /// <param name="propertyItem">The property item to create an editor for</param>
    /// <returns>A UI element that can edit the property</returns>
    UIElement CreateEditor(PropertyHierarchyItem propertyItem);

    /// <summary>
    /// Determines whether this editor can handle the specified property item.
    /// </summary>
    /// <param name="propertyItem">The property item to check</param>
    /// <returns>True if this editor can handle the property, false otherwise</returns>
    bool CanHandle(PropertyHierarchyItem propertyItem);
} 