using System.Collections.Specialized;
using System.Management;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property descriptor implementation that handles WMI-specific properties from ManagementBaseObject.
/// </summary>
public class WmiPropertyDescriptor : IPropertyDescriptor
{
    private readonly bool _allowExpansion;
    private readonly string _category;
    private readonly object? _context;
    private readonly bool _forceEditable;
    private readonly PropertyData _propertyData;
    private readonly IPropertyGridContext? _propertyGridContext;
    private readonly ManagementBaseObject _source;
    private readonly WmiProperty _wmiProperty;
    private ManagementObject? _cachedReferenceObject;

    public WmiPropertyDescriptor(PropertyData propertyData, ManagementBaseObject source, string category, bool allowExpansion = false, IPropertyGridContext? propertyGridContext = null, bool forceEditable = false)
    {
        _propertyData = propertyData ?? throw new ArgumentNullException(nameof(propertyData));
        _source = source;
        _allowExpansion = allowExpansion;
        _category = category;
        _propertyGridContext = propertyGridContext;
        _forceEditable = forceEditable;

        // Use the same logic as before to get the class definition for WmiProperty
        ManagementClass? parentClass = null;
        try
        {
            var classPath = _source.ClassPath?.Path;
            if (!string.IsNullOrEmpty(classPath))
            {
                var scope = _source is ManagementObject mo ? mo.Scope : null;
                var options = new ObjectGetOptions(null, TimeSpan.MaxValue, true);
                parentClass = new ManagementClass(scope, new ManagementPath(classPath), options);
            }
        }
        catch
        {
            // Ignore errors, parentClass will be null
        }
        _wmiProperty = new WmiProperty(_propertyData, parentClass);
    }

    // New constructor for explicit context
    public WmiPropertyDescriptor(PropertyData propertyData, ManagementBaseObject source, string category, object? context, bool allowExpansion = false, IPropertyGridContext? propertyGridContext = null, bool forceEditable = false)
        : this(propertyData, source, category, allowExpansion, propertyGridContext, forceEditable)
    {
        _context = context;
    }

    public string Category => _category;
    public string Description => GetPropertyDescription();
    public string DisplayName => _wmiProperty.Name;
    public bool IsKey => _wmiProperty.IsKey;
    public bool IsObject => _propertyData.Type == CimType.Object;
    public bool IsReadOnly => _forceEditable ? false : _wmiProperty.IsReadOnly;
    public bool IsReference => _propertyData.Type == CimType.Reference;
    public string Name => _wmiProperty.Name;
    public PropertyData PropertyData => _propertyData;
    public Type? PropertyType
    {
        get
        {
            if (_allowExpansion)
                return typeof(PropertyData);

            // If this is a Reference type and we've successfully converted it to a ManagementObject, return ManagementObject type
            if (_propertyData.Type == CimType.Reference && _cachedReferenceObject != null)
                return typeof(ManagementObject);

            return GetTypeForCimType(_propertyData.Type, _propertyData.IsArray);
        }
    }
    public object Source => _source;
    public object? Value => GetValue();
    public WmiProperty WmiProperty => _wmiProperty;

    public ManagementScope? GetManagementScope()
    {
        if (_source is ManagementObject managementObject)
        {
            return managementObject.Scope;
        }
        else if (_source is ManagementClass managementClass)
        {
            return managementClass.Scope;
        }
        return null;
    }

    public bool SetValue(object? value)
    {
        if (IsReadOnly || _source == null)
            return false;
        try
        {
            var convertedValue = ConvertValueToPropertyType(value, _propertyData.Type, _propertyData.IsArray);
            _propertyData.Value = convertedValue;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set value {Value} for property '{PropertyName}' on {ClassName}.", value!, _propertyData?.Name!, _source.ClassPath?.ClassName!);
            return false;
        }
    }

    // Helper for value conversion (copied from previous logic)
    private object? ConvertValueToPropertyType(object? value, CimType cimType, bool isArray)
    {
        if (value == null)
            return null;
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue) && cimType != CimType.String)
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
                    CimType.Char16 => stringValue,
                    CimType.DateTime => stringValue,
                    _ => stringValue
                };
            }
            catch (Exception ex)
            {
                Log.Error("Failed to convert string '{StringValue}' to CIM type '{CimType}' for property '{PropertyName}': {ErrorMessage}",
                    stringValue, cimType, _propertyData.Name, ex.Message);
                throw;
            }
        }
        return value;
    }

    // Helper to format integer values as hex
    private static string FormatIntegerAsHex(object value, Type? propertyType)
    {
        if (value == null) return string.Empty;

        try
        {
            return propertyType switch
            {
                var t when t == typeof(byte) || t == typeof(byte?) =>
                    $"0x{Convert.ToByte(value):X2}",
                var t when t == typeof(sbyte) || t == typeof(sbyte?) =>
                    $"0x{(byte)Convert.ToSByte(value):X2}",
                var t when t == typeof(ushort) || t == typeof(ushort?) =>
                    $"0x{Convert.ToUInt16(value):X4}",
                var t when t == typeof(short) || t == typeof(short?) =>
                    $"0x{(ushort)Convert.ToInt16(value):X4}",
                var t when t == typeof(uint) || t == typeof(uint?) =>
                    $"0x{Convert.ToUInt32(value):X8}",
                var t when t == typeof(int) || t == typeof(int?) =>
                    $"0x{(uint)Convert.ToInt32(value):X8}",
                var t when t == typeof(ulong) || t == typeof(ulong?) =>
                    $"0x{Convert.ToUInt64(value):X16}",
                var t when t == typeof(long) || t == typeof(long?) =>
                    $"0x{(ulong)Convert.ToInt64(value):X16}",
                var t when t == typeof(char) || t == typeof(char?) =>
                    $"0x{Convert.ToUInt16(value):X4}", // Char as UTF-16 hex
                _ => string.Empty
            };
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    // Enhanced value display using possible values (delegates to WmiProperty, always fresh)
    private object? GetEnhancedValue(object? rawValue)
    {
        if (rawValue == null)
            return null;
        NameValueCollection? possibleValues = null;
        // Use explicit context if available and appropriate
        object? context = _context;
        if (_propertyData.Origin == "__PARAMETERS")
        {
            if (context is WmiMethod ctxMethod)
            {
                possibleValues = ctxMethod.PossibleValues;
            }
        }
        else
        {
            possibleValues = _wmiProperty.PossibleValues;
        }

        if (possibleValues != null && possibleValues.Count > 0)
        {
            var valueStr = rawValue.ToString();
            if (string.IsNullOrEmpty(valueStr))
                return null;
            var allKeys = possibleValues.AllKeys;
            if (allKeys == null)
                return null;
            // Try to match by key (code)
            foreach (string? key in allKeys)
            {
                if (key == valueStr)
                {
                    var displayValue = possibleValues[key];
                    if (displayValue != null && displayValue != key)
                        return $"{key} [{displayValue}]";
                    return key;
                }
            }
            // Try to match by value (display string)
            foreach (string? key in allKeys)
            {
                var displayValue = possibleValues[key];
                if (displayValue == valueStr)
                {
                    if (key != displayValue)
                        return $"{key} [{displayValue}]";
                    return displayValue;
                }
            }
        }
        // Fallback to hex if appropriate
        var propertyType = PropertyType;
        var hex = FormatIntegerAsHex(rawValue, propertyType);
        if (!string.IsNullOrEmpty(hex))
        {
            return $"{rawValue} [{hex}]";
        }
        return rawValue;
    }

    private string GetPropertyDescription()
    {
        // Add the type information
        string typeLine = $"Type: {_propertyData.Type}{(_propertyData.IsArray ? " (Array)" : "")}";
        if (_wmiProperty.IsKey)
        {
            typeLine += " [Key Property]";
        }

        // Use the optimized description (instance or parent class)
        string? description = _wmiProperty.Description;

        // If it's a referenced or embedded instance/object, add concise information
        if (_propertyData.Type == CimType.Reference || _propertyData.Type == CimType.Object)
        {
            string? cimType = _wmiProperty.CimType?.ToString();
            if (_propertyData.Type == CimType.Reference)
            {
                if (!string.IsNullOrEmpty(cimType))
                {
                    typeLine += $" (Referenced instance of {cimType})";
                }
                else
                {
                    typeLine += " (Referenced instance)";
                }
            }
            else if (_propertyData.Type == CimType.Object)
            {
                if (!string.IsNullOrEmpty(cimType))
                {
                    typeLine += $" (Embedded instance of {cimType})";
                }
                else
                {
                    typeLine += " (Embedded instance)";
                }
            }
        }

        // Add enhanced value information if available
        var enhancedValue = GetEnhancedValue(_propertyData.Value);
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

    // Helper for .NET type mapping (copied from previous logic)
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

    // Value logic, including expansion and enhanced value display
    private object? GetValue()
    {
        if (_allowExpansion)
            return _propertyData;

        var rawValue = _wmiProperty.Value;

        // Handle Reference type properties with string values - convert to ManagementObject
        if (IsReference && _propertyGridContext?.IsReadOnly == true && rawValue is string pathString && !string.IsNullOrEmpty(pathString))
        {
            // Return cached object if available
            if (_cachedReferenceObject != null)
                return _cachedReferenceObject;

            // Try to convert string path to ManagementObject
            try
            {
                var path = new ManagementPath(pathString);

                // Validate that the path represents an object path (not a class or namespace path)
                if (path.IsInstance && !string.IsNullOrEmpty(path.ClassName))
                {
                    // Get the scope from source
                    ManagementScope? scope = GetManagementScope();
                    if (scope != null)
                    {
                        var mObject = new ManagementObject(scope, path, new ObjectGetOptions());
                        mObject.Get(); // Load properties
                        _cachedReferenceObject = mObject;
                        return mObject;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but fall through to return the original string value
                Log.Warning(ex, "Failed to convert Reference path string to ManagementObject for property '{PropertyName}': {PathString}", _propertyData.Name, pathString);
            }
        }

        if (_propertyGridContext?.IsReadOnly == true && _propertyData.Type == CimType.DateTime && rawValue is string s && !string.IsNullOrEmpty(s))
        {
            var dt = ManagementDateTimeConverter.ToDateTime(s);
            return $"{dt:G} [{s}]";
        }
        // Only compute enhanced value if the property grid is read-only
        if (_propertyGridContext?.IsReadOnly == true)
        {
            var enhancedValue = GetEnhancedValue(rawValue);
            if (enhancedValue != null)
                return enhancedValue;
        }
        return rawValue;
    }
}