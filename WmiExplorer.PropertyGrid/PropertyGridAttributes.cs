using System.Reflection;
using WmiExplorer.PropertyGrid.Providers;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Indicates that a property should be expanded by default.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExpandByDefaultAttribute : Attribute
{
}

/// <summary>
/// Helper methods for working with property grid attributes.
/// </summary>
public static class PropertyGridAttributeHelpers
{
    /// <summary>
    /// Gets a specific attribute from the property descriptor, or null if not present.
    /// </summary>
    public static TAttribute? GetPropertyAttribute<TAttribute>(Abstractions.IPropertyDescriptor descriptor) where TAttribute : Attribute
    {
        PropertyInfo? propInfo = null;
        if (descriptor is DefaultPropertyDescriptor rpd)
        {
            propInfo = rpd.PropertyInfo;
        }
        else
        {
            var pi = descriptor.GetType().GetProperty("PropertyInfo");
            if (pi != null)
                propInfo = pi.GetValue(descriptor) as PropertyInfo;
        }
        return propInfo != null ? propInfo.GetCustomAttribute<TAttribute>() : null;
    }

    /// <summary>
    /// Checks if the property descriptor has a specific attribute.
    /// </summary>
    public static bool HasPropertyAttribute<TAttribute>(Abstractions.IPropertyDescriptor descriptor) where TAttribute : Attribute
    {
        return GetPropertyAttribute<TAttribute>(descriptor) != null;
    }
}

/// <summary>
/// Indicates that a property should be expanded by default in the property grid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ShowChildrenAsParentAttribute : Attribute
{
}