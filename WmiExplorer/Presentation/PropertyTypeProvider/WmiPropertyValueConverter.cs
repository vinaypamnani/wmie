using System.Management;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    /// <summary>
    /// Converter for WMI-specific value formatting and editing.
    /// </summary>
    public class WmiPropertyValueConverter : IPropertyValueConverter
    {
        /// <summary>
        /// Gets the priority of this converter.
        /// </summary>
        public int Priority => 100; // Higher priority than default converter

        /// <summary>
        /// Determines if this converter can handle the specified property type.
        /// </summary>
        public bool CanConvert(Type? propertyType)
        {
            if (propertyType == null)
                return false;

            // Check if this is a WMI type
            return typeof(ManagementBaseObject).IsAssignableFrom(propertyType) ||
                   typeof(PropertyData).IsAssignableFrom(propertyType) ||
                   typeof(QualifierData).IsAssignableFrom(propertyType) ||
                   typeof(ManagementPath).IsAssignableFrom(propertyType) ||
                   typeof(ManagementScope).IsAssignableFrom(propertyType);
        }

        /// <summary>
        /// Converts a string value back to the property's type.
        /// </summary>
        public object? ConvertFromString(string value, Type propertyType)
        {
            // Most WMI types don't support conversion from string
            return null;
        }

        /// <summary>
        /// Converts a property value to a string for display.
        /// </summary>
        public string ConvertToString(object? value, Type propertyType)
        {
            if (value == null)
                return "<null>";

            // Special handling for WMI types
            if (value is ManagementBaseObject mbo)
            {
                // Try __RELPATH value
                var relPath = mbo.SystemProperties["__RELPATH"]?.Value as string;
                if (!string.IsNullOrEmpty(relPath))
                    return $"[Embedded: {relPath}]";

                // Fallback: use ClassPath.Path
                var classPath = mbo.ClassPath?.Path ?? "Object";
                string? displayName = null;
                // Try to get DisplayName or Name property value
                if (mbo.Properties["DisplayName"] != null && mbo.Properties["DisplayName"].Value is string dn && !string.IsNullOrEmpty(dn))
                    displayName = dn;
                else if (mbo.Properties["Name"] != null && mbo.Properties["Name"].Value is string n && !string.IsNullOrEmpty(n))
                    displayName = n;

                if (!string.IsNullOrEmpty(displayName))
                    return $"[Embedded: {classPath} ({displayName})]";
                else
                    return $"[Embedded: {classPath}]";
            }
            if (value is ManagementBaseObject[] mboArray)
            {
                return $"[Embedded Array: {mboArray.Length} object(s)]";
            }
            if (value is ManagementPath)
                return "ManagementPath";

            if (value is ManagementScope)
                return "ManagementScope";

            if (value is ObjectGetOptions)
                return "ObjectGetOptions";

            if (value is ConnectionOptions)
                return "ConnectionOptions";

            if (value is PropertyData propertyData)
            {
                if (propertyData.IsArray && propertyData.Value is Array pdArray)
                    return $"{propertyData.Type} Array[{pdArray.Length}]";

                return propertyData.Value?.ToString() ?? "<null>";
            }

            if (value is QualifierData qualifierData)
                return qualifierData.Value?.ToString() ?? "<null>";

            if (value is PropertyDataCollection)
                return "PropertyDataCollection";

            if (value is QualifierDataCollection)
                return "QualifierDataCollection";

            // Default to ToString for other types
            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets a value indicating whether the specified property type should be edited with a custom editor.
        /// </summary>
        public bool RequiresCustomEditor(Type? propertyType)
        {
            // WMI types generally can't be edited directly
            return false;
        }
    }
}