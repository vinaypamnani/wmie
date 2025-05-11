namespace WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions
{
    /// <summary>
    /// Interface for providers that handle specific types of properties.
    /// Implementations can handle standard .NET types, WMI types, or custom types.
    /// </summary>
    public interface IPropertyTypeProvider
    {
        /// <summary>
        /// Determines if this provider can handle the specified object type.
        /// </summary>
        /// <param name="objectType">The type of the object to check</param>
        /// <returns>True if this provider can handle the type, false otherwise</returns>
        bool CanHandle(Type objectType);

        /// <summary>
        /// Gets child items for an expandable property value (collection or complex object).
        /// </summary>
        /// <param name="value">The value to get child items for</param>
        /// <param name="parentName">The name of the parent property</param>
        /// <param name="parentCategory">The category of the parent property</param>
        /// <returns>A collection of property descriptors representing the child items</returns>
        IEnumerable<IPropertyDescriptor> GetChildItems(object value, string parentName, string parentCategory);

        /// <summary>
        /// Gets all property descriptors for the specified object.
        /// </summary>
        /// <param name="obj">The object to get property descriptors for</param>
        /// <returns>A collection of property descriptors</returns>
        IEnumerable<IPropertyDescriptor> GetProperties(object obj);

        /// <summary>
        /// Determines if the specified value represents a collection or complex object that can be expanded.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <param name="valueType">The type of the value</param>
        /// <returns>True if the value is expandable, false otherwise</returns>
        bool IsExpandable(object value, Type valueType);
    }
}