using System.Management;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    /// <summary>
    /// Property descriptor implementation that handles WMI-specific properties from ManagementBaseObject.
    /// </summary>
    public class WmiPropertyDescriptor : IPropertyDescriptor
    {
        private readonly bool _isKey;
        private readonly bool _isReadOnly;
        private readonly PropertyData _propertyData;
        private readonly ManagementBaseObject _source;

        /// <summary>
        /// Creates a new WmiPropertyDescriptor instance.
        /// </summary>
        public WmiPropertyDescriptor(PropertyData propertyData, ManagementBaseObject source)
        {
            _propertyData = propertyData ?? throw new ArgumentNullException(nameof(propertyData));
            _source = source; // Allow null source for system properties

            // Determine if this is a system property or regular property
            Category = propertyData.Origin?.StartsWith("___") == true ? "System Properties" : "Properties";

            // Determine if property is read-only (generally true for WMI properties)
            _isReadOnly = true;

            // Check if this is a key property
            _isKey = GetQualifierValue("key") != null;

            // Get property description from qualifiers
            Description = GetPropertyDescription();
        }

        /// <summary>
        /// Gets the category of the property (System Properties or Properties).
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets the description of the property.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the display name of the property, with a special marker (*) for key properties.
        /// </summary>
        public string DisplayName => _isKey ? $"*{Name}" : Name;

        /// <summary>
        /// Gets whether the property is read-only.
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name => _propertyData.Name;

        /// <summary>
        /// Gets the type of the property based on CIM type.
        /// </summary>
        public Type PropertyType => GetTypeForCimType(_propertyData.Type, _propertyData.IsArray);

        /// <summary>
        /// Gets the source object containing this property.
        /// </summary>
        public object Source => _source;

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public object Value => _propertyData.Value;

        /// <summary>
        /// Gets property description from the class definition using UseAmendedQualifiers
        /// </summary>
        private string? GetDescriptionFromClass()
        {
            try
            {
                // Only try this if we have a source and it's not a system property
                if (_source == null || _propertyData.Origin?.StartsWith("___") == true)
                    return null;

                // Get class name from source
                var classPath = _source.ClassPath?.Path;
                if (string.IsNullOrEmpty(classPath))
                    return null;

                // Get the scope from the source if available
                var scope = _source is ManagementObject mo ? mo.Scope : null;

                // Create options with UseAmendedQualifiers set to true
                var options = new ObjectGetOptions(null, TimeSpan.MaxValue, true);

                // Get the class definition
                using var classDefinition = new ManagementClass(scope, new ManagementPath(classPath), options);

                // Find the property in the class
                if (classDefinition.Properties != null)
                {
                    PropertyData? classProperty = null;
                    foreach (PropertyData prop in classDefinition.Properties)
                    {
                        if (prop.Name.Equals(_propertyData.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            classProperty = prop;
                            break;
                        }
                    }

                    // Get the description qualifier if the property was found
                    if (classProperty != null)
                    {
                        return GetQualifierValue(classProperty, "description")?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any errors but don't let them propagate
                System.Diagnostics.Debug.WriteLine($"Error getting property description from class: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets a description from PropertyData qualifiers.
        /// </summary>
        private string GetPropertyDescription()
        {
            // Add the type information
            string typeLine = $"Type: {_propertyData.Type}{(_propertyData.IsArray ? " (Array)" : "")}";

            // Get description from qualifiers
            string? description = GetQualifierValue("description")?.ToString();

            // If not found directly, try to get it from the class definition with UseAmendedQualifiers
            if (string.IsNullOrEmpty(description))
            {
                description = GetDescriptionFromClass();
            }

            // If it's a referenced instance (embedded object), add that information
            if (_propertyData.Type == CimType.Object || _propertyData.Type == CimType.Reference)
            {
                string? cimType = GetQualifierValue("cimtype")?.ToString();
                if (!string.IsNullOrEmpty(cimType))
                {
                    typeLine += $" (Instance of {cimType})";
                }
                else
                {
                    typeLine += " (Referenced WMI instance)";
                }
            }

            // Compose the description: type line first, then description if present
            string result = typeLine;
            if (!string.IsNullOrEmpty(description))
            {
                result += $"\n{description}";
            }
            return result;
        }

        /// <summary>
        /// Helper method to get a qualifier value by name
        /// </summary>
        private object? GetQualifierValue(string qualifierName)
        {
            return GetQualifierValue(_propertyData, qualifierName);
        }

        /// <summary>
        /// Helper method to get a qualifier value by name from specified PropertyData
        /// </summary>
        private object? GetQualifierValue(PropertyData propertyData, string qualifierName)
        {
            if (propertyData.Qualifiers == null)
                return null;

            foreach (QualifierData qualifier in propertyData.Qualifiers)
            {
                if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
                {
                    return qualifier.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Converts a CIM type to a .NET type.
        /// </summary>
        private Type GetTypeForCimType(CimType cimType, bool isArray)
        {
            switch (cimType)
            {
                case CimType.SInt8:
                    return isArray ? typeof(sbyte[]) : typeof(sbyte);

                case CimType.UInt8:
                    return isArray ? typeof(byte[]) : typeof(byte);

                case CimType.SInt16:
                    return isArray ? typeof(short[]) : typeof(short);

                case CimType.UInt16:
                    return isArray ? typeof(ushort[]) : typeof(ushort);

                case CimType.SInt32:
                    return isArray ? typeof(int[]) : typeof(int);

                case CimType.UInt32:
                    return isArray ? typeof(uint[]) : typeof(uint);

                case CimType.SInt64:
                    return isArray ? typeof(long[]) : typeof(long);

                case CimType.UInt64:
                    return isArray ? typeof(ulong[]) : typeof(ulong);

                case CimType.Real32:
                    return isArray ? typeof(float[]) : typeof(float);

                case CimType.Real64:
                    return isArray ? typeof(double[]) : typeof(double);

                case CimType.Boolean:
                    return isArray ? typeof(bool[]) : typeof(bool);

                case CimType.String:
                    return isArray ? typeof(string[]) : typeof(string);

                case CimType.DateTime:
                    return isArray ? typeof(DateTime[]) : typeof(DateTime);

                case CimType.Reference:
                    return isArray ? typeof(object[]) : typeof(object);

                case CimType.Char16:
                    return isArray ? typeof(char[]) : typeof(char);

                case CimType.Object:
                    return isArray ? typeof(object[]) : typeof(object);

                default:
                    return typeof(object);
            }
        }

        /// <summary>
        /// Sets the value of the property if it is writable.
        /// </summary>
        public bool SetValue(object? value)
        {
            // Most WMI properties are read-only in this context
            if (_isReadOnly || _source == null)
                return false;

            try
            {
                // For writable properties, set the value and return success/failure
                if (_source is ManagementObject managementObject)
                {
                    _propertyData.Value = value;
                    managementObject.Put();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting WMI property value: {ex.Message}");
                return false;
            }
        }
    }
}