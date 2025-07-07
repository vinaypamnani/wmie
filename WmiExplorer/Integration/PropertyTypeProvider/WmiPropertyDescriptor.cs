using System.Management;
using WmiExplorer.Common.Logging;
using WmiExplorer.Core.Models;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property descriptor implementation that handles WMI-specific properties from ManagementBaseObject.
/// </summary>
public class WmiPropertyDescriptor : IPropertyDescriptor
{
    private readonly bool _allowExpansion;

    // Cache for WmiProperty to avoid repeated computation
    private WmiProperty? _cachedWmiProperty;

    private readonly string _category;
    private readonly IPropertyGridContext? _propertyGridContext;
    private readonly bool _isKey;
    private readonly bool _isReadOnly;
    private readonly PropertyData _propertyData;
    private readonly ManagementBaseObject _source;
    private bool _wmiPropertyCached;

    /// <summary>
    /// Creates a new WmiPropertyDescriptor instance.
    /// </summary>
    public WmiPropertyDescriptor(PropertyData propertyData, ManagementBaseObject source, string category, bool allowExpansion = false, IPropertyGridContext? propertyGridContext = null)
    {
        _propertyData = propertyData ?? throw new ArgumentNullException(nameof(propertyData));
        _source = source; // Allow null source for system properties
        _allowExpansion = allowExpansion;
        _category = category;
        _propertyGridContext = propertyGridContext;

        // Auto-detect read-only status based on context and qualifiers
        _isReadOnly = DetermineReadOnlyStatus();

        // Check if this is a key property
        _isKey = GetQualifierFromClassOrInstance("key") != null;

        // Get property description from qualifiers
        Description = GetPropertyDescription();
    }

    /// <summary>
    /// Gets the category of the property.
    /// </summary>
    public string Category => _category;

    /// <summary>
    /// Gets the description of the property.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the display name of the property.
    /// </summary>
    public string DisplayName => Name;

    /// <summary>
    /// Gets whether this property is a key property.
    /// </summary>
    public bool IsKey => _isKey;

    /// <summary>
    /// Gets whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => _isReadOnly;

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name => _propertyData.Name;

    /// <summary>
    /// Gets the type of the property based on CIM type. Allows expansion if _allowExpansion is true (for WmiClass).
    /// </summary>
    public Type PropertyType => _allowExpansion ? typeof(PropertyData) : GetTypeForCimType(_propertyData.Type, _propertyData.IsArray);

    /// <summary>
    /// Gets the source object containing this property.
    /// </summary>
    public object Source => _source;

    /// <summary>
    /// Gets the value of the property. Allows expansion if _allowExpansion is true (for WmiClass).
    /// </summary>
    public object Value
    {
        get
        {
            if (_allowExpansion)
                return _propertyData;

            var rawValue = _propertyData.Value;

            // Handle DateTime formatting
            if (_propertyData.Type == CimType.DateTime && rawValue is string s && !string.IsNullOrEmpty(s))
            {
                var dt = ManagementDateTimeConverter.ToDateTime(s);
                return $"{dt:G} [{s}]";
            }

            // Only compute enhanced value if the property grid is read-only
            if (_propertyGridContext?.IsReadOnly == true)
            {
                var enhancedValue = GetEnhancedValueWithPossibleValues(_propertyData.Value);
                if (enhancedValue != null)
                {
                    return enhancedValue;
                }
            }

            return rawValue;
        }
    }

    /// <summary>
    /// Sets the value of the property if it is writable.
    /// </summary>
    public bool SetValue(object? value)
    {
        if (_isReadOnly || _source == null)
        {
            return false;
        }

        try
        {
            // Convert value to appropriate type if needed
            var convertedValue = ConvertValueToPropertyType(value, _propertyData.Type, _propertyData.IsArray);

            // Set the value on the property
            _propertyData.Value = convertedValue;

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to set value for property '{_propertyData.Name}' on {_source.ClassPath?.ClassName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Converts a value to the appropriate type for the WMI property.
    /// </summary>
    private object? ConvertValueToPropertyType(object? value, CimType cimType, bool isArray)
    {
        if (value == null)
            return null;

        // Handle string input that needs conversion
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
                return null;

            try
            {
                return cimType switch
                {
                    CimType.Boolean => bool.Parse(stringValue),
                    CimType.SInt16 => short.Parse(stringValue),
                    CimType.UInt16 => ushort.Parse(stringValue),
                    CimType.SInt32 => int.Parse(stringValue),
                    CimType.UInt32 => uint.Parse(stringValue),
                    CimType.SInt64 => long.Parse(stringValue),
                    CimType.UInt64 => ulong.Parse(stringValue),
                    CimType.Real32 => float.Parse(stringValue),
                    CimType.Real64 => double.Parse(stringValue),
                    CimType.String => stringValue,
                    CimType.DateTime => ManagementDateTimeConverter.ToDateTime(stringValue),
                    _ => stringValue
                };
            }
            catch
            {
                // If conversion fails, return the original string
                return stringValue;
            }
        }

        return value;
    }

    /// <summary>
    /// Creates a WmiProperty instance by finding the matching property in the class definition.
    /// </summary>
    private WmiProperty? CreateWmiProperty()
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

            // Find the matching property in the class
            if (classDefinition.Properties != null)
            {
                foreach (PropertyData prop in classDefinition.Properties)
                {
                    if (prop.Name.Equals(_propertyData.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        // Create WmiProperty with the class definition property and parent class
                        return new WmiProperty(prop, classDefinition);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error creating WmiProperty for property '{PropertyName}' in class '{ClassName}'",
                _propertyData.Name, _source?.ClassPath?.ClassName ?? "Unknown");
        }

        return null;
    }

    /// <summary>
    /// Determines if the property should be read-only based on qualifiers.
    /// First checks instance qualifiers, then falls back to class definition qualifiers.
    /// </summary>
    private bool DetermineReadOnlyStatus()
    {
        // Check if property has Write qualifier - this is the authoritative source
        var writeQualifier = GetQualifierFromClassOrInstance("write");
        if (writeQualifier is bool writeBool)
        {
            return !writeBool; // If write=true, then not read-only
        }

        // Check if property is marked as read-only
        var readOnlyQualifier = GetQualifierFromClassOrInstance("read");
        if (readOnlyQualifier is bool readBool && readBool)
        {
            return true;
        }

        // Default: For regular WMI instances without explicit qualifiers, properties are generally read-only
        return true;
    }

    /// <summary>
    /// Gets an enhanced value display using possible values if available.
    /// </summary>
    private object? GetEnhancedValueWithPossibleValues(object? rawValue)
    {
        if (rawValue == null)
            return null;

        // Get the WmiProperty for this property to access PossibleValues
        var wmiProperty = GetWmiProperty();
        if (wmiProperty?.PossibleValues == null || wmiProperty.PossibleValues.Count == 0)
            return null;

        // Convert raw value to string for comparison
        var valueStr = rawValue.ToString();
        if (string.IsNullOrEmpty(valueStr))
            return null;

        // Look for a matching value in possible values
        var allKeys = wmiProperty.PossibleValues.AllKeys;
        if (allKeys == null) return null;

        foreach (string? key in allKeys)
        {
            if (key == null) continue;

            var possibleValue = wmiProperty.PossibleValues[key];
            if (string.Equals(possibleValue, valueStr, StringComparison.OrdinalIgnoreCase))
            {
                // If we have a mapped value that's different from the key, show both
                if (!string.IsNullOrEmpty(possibleValue) && !string.Equals(key, possibleValue, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{possibleValue} [{key}]";
                }
                // If key equals mapped value, we don't need to enhance
                return null;
            }
        }

        // No match found, return null to use raw value
        return null;
    }

    /// <summary>
    /// Gets a description from PropertyData qualifiers.
    /// </summary>
    private string GetPropertyDescription()
    {
        // Add the type information
        string typeLine = $"Type: {_propertyData.Type}{(_propertyData.IsArray ? " (Array)" : "")}";
        if (_isKey)
        {
            typeLine += " [Key Property]";
        }

        // Get description from qualifiers (instance first, then class definition)
        string? description = GetQualifierFromClassOrInstance("description")?.ToString();

        // If it's a referenced instance (embedded object), add that information
        if (_propertyData.Type == CimType.Object || _propertyData.Type == CimType.Reference)
        {
            string? cimType = GetQualifierFromClassOrInstance("cimtype")?.ToString();
            if (!string.IsNullOrEmpty(cimType))
            {
                typeLine += $" (Instance of {cimType})";
            }
            else
            {
                typeLine += " (Referenced WMI instance)";
            }
        }

        // Add enhanced value information if available
        var enhancedValue = GetEnhancedValueWithPossibleValues(_propertyData.Value);
        if (enhancedValue != null)
        {
            typeLine += $"; Value: {enhancedValue}";
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
    /// Gets a qualifier value from the class definition using UseAmendedQualifiers.
    /// </summary>
    private object? GetQualifierFromClass(string qualifierName)
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

                // Get the qualifier value if the property was found
                if (classProperty != null)
                {
                    return GetQualifierValue(classProperty, qualifierName);
                }
            }
        }
        catch (Exception ex)
        {
            // Log any errors but don't let them propagate
            Log.Warning(ex, $"Error getting qualifier '{qualifierName}' from class definition for property '{_propertyData.Name}' in class '{_source.ClassPath?.ClassName ?? "Unknown"}'");
        }

        return null;
    }

    /// <summary>
    /// Gets a qualifier value from either the instance property or class definition.
    /// First checks the instance property, then falls back to the class definition with UseAmendedQualifiers.
    /// </summary>
    private object? GetQualifierFromClassOrInstance(string qualifierName)
    {
        // First try to get the qualifier from the instance property
        var instanceValue = GetQualifierValue(qualifierName);
        if (instanceValue != null)
        {
            return instanceValue;
        }

        // Fall back to class definition
        return GetQualifierFromClass(qualifierName);
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
    /// Gets or creates a WmiProperty instance for this property from the class definition.
    /// </summary>
    private WmiProperty? GetWmiProperty()
    {
        if (!_wmiPropertyCached)
        {
            _cachedWmiProperty = CreateWmiProperty();
            _wmiPropertyCached = true;
        }

        return _cachedWmiProperty;
    }
}