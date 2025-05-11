namespace WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions
{
    /// <summary>
    /// Interface for converters that can format property values for display and convert edited values back to their original type.
    /// </summary>
    public interface IPropertyValueConverter
    {
        /// <summary>
        /// Gets a priority value for this converter (higher priority converters are tried first).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Determines if this converter can handle the specified property type.
        /// </summary>
        /// <param name="propertyType">The type of property to check</param>
        /// <returns>True if this converter can handle the type, false otherwise</returns>
        bool CanConvert(Type? propertyType);

        /// <summary>
        /// Converts a string value back to the property's type.
        /// </summary>
        /// <param name="value">The string value to convert</param>
        /// <param name="propertyType">The target type</param>
        /// <returns>The converted value</returns>
        object? ConvertFromString(string value, Type propertyType);

        /// <summary>
        /// Converts a property value to a string for display.
        /// </summary>
        /// <param name="value">The property value to convert</param>
        /// <param name="propertyType">The type of the property</param>
        /// <returns>A string representation of the value</returns>
        string ConvertToString(object? value, Type propertyType);

        /// <summary>
        /// Gets a value indicating whether the specified property type should be edited with a custom editor.
        /// </summary>
        /// <param name="propertyType">The property type to check</param>
        /// <returns>True if the property should use a custom editor, false otherwise</returns>
        bool RequiresCustomEditor(Type? propertyType);
    }
}