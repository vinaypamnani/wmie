using System.Management;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

/// <summary>
/// Factory for creating and cleaning WMI objects for method parameter use.
/// Handles the complex logic of creating template objects and preparing them for WMI method calls.
/// </summary>
public static class WmiObjectFactory
{
    /// <summary>
    /// Cleans a parameter object for WMI method calls.
    /// Processes PropertyGrid values, converting "&lt;null&gt;" strings back to null and filtering meaningful values.
    /// </summary>
    /// <param name="source">The ManagementObject to clean</param>
    /// <returns>The same object with cleaned properties</returns>
    public static ManagementObject CleanParameterObject(ManagementObject source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        try
        {
            Log.Debug("Cleaning parameter object properties for: {ClassName}", source.ClassPath?.ClassName ?? "Unknown");

            // Process properties: convert "<null>" back to null and identify meaningful values
            int meaningfulPropertyCount = 0;
            foreach (PropertyData prop in source.Properties)
            {
                if (prop.Name.StartsWith("__"))
                    continue;

                try
                {
                    // Check if this property has a meaningful value (this handles null conversion internally)
                    if (HasMeaningfulValue(prop))
                    {
                        Log.Debug("Meaningful value found for property {PropertyName}: {Value}", prop.Name, prop.Value);
                        meaningfulPropertyCount++;
                    }
                }
                catch (Exception propEx)
                {
                    Log.Error(propEx, "Error processing property {PropertyName} in WMI object {ClassName}", prop.Name, source.ClassPath?.ClassName ?? "Unknown");
                }
            }

            Log.Debug("Parameter object {ClassName} has {Count} meaningful properties after cleaning.", source.ClassPath?.ClassName ?? "Unknown", meaningfulPropertyCount);
            return source;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clean parameter object for class: {ClassName}", source.ClassPath?.ClassName ?? "Unknown");
            return source; // Return as-is if cleaning fails
        }
    }

    /// <summary>
    /// Creates a template WMI object for method parameters.
    /// This avoids the issues with CreateInstance() for abstract or non-instantiable classes.
    /// </summary>
    /// <param name="className">The WMI class name to create a template for</param>
    /// <param name="scope">The management scope</param>
    /// <returns>A ManagementObject that can be used for parameter editing</returns>
    public static ManagementObject CreateTemplateObject(string className, ManagementScope scope)
    {
        try
        {
            Log.Debug("Creating template object for WMI class: {ClassName}", className);

            var classPath = new ManagementPath($"{scope.Path.Path}:{className}");
            var managementClass = new ManagementClass(scope, classPath, null);

            // First try to create an actual instance (works for some classes)
            try
            {
                var instance = managementClass.CreateInstance();
                Log.Debug("Created actual instance for WMI class: {ClassName}", className);
                return instance;
            }
            catch (Exception createEx)
            {
                Log.Warning(createEx, "CreateInstance failed for WMI class: {ClassName}. Falling back to manual template creation.", className);
                return CreateManualTemplateObject(className, managementClass);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create template object for WMI class: {ClassName}", className);
            throw new InvalidOperationException($"Failed to create template object for class '{className}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts a string value to the specified type
    /// </summary>
    private static object? ConvertStringToType(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(value);
        }

        return Convert.ChangeType(value, targetType);
    }

    /// <summary>
    /// Creates a manual template object by copying properties from the management class.
    /// Used when CreateInstance() fails for abstract or non-instantiable classes.
    /// </summary>
    private static ManagementObject CreateManualTemplateObject(string className, ManagementClass managementClass)
    {
        var templateObject = new ManagementObject();

        foreach (PropertyData classProperty in managementClass.Properties)
        {
            try
            {
                // Skip system properties that might cause issues
                if (classProperty.Name.StartsWith("__"))
                    continue;

                // For method parameters, start with null values - only user-set properties will have values
                templateObject.Properties.Add(classProperty.Name, null, classProperty.Type);
            }
            catch (Exception propEx)
            {
                Log.Warning(propEx, "Error copying property '{PropertyName}' for WMI class '{ClassName}'", classProperty.Name, className);
            }
        }

        Log.Debug("Created manual template object for WMI class: {ClassName} with {PropertyCount} properties", className, templateObject.Properties.Count);
        return templateObject;
    }

    /// <summary>
    /// Gets the .NET element type from a WMI CIM type string for arrays
    /// </summary>
    private static Type? GetArrayElementTypeFromCimType(string cimType)
    {
        // Remove array indicators and get base type
        var baseType = cimType.Replace("[]", "").Trim();

        return baseType.ToLowerInvariant() switch
        {
            "string" => typeof(string),
            "sint32" or "int32" or "uint32" => typeof(int),
            "sint64" or "int64" or "uint64" => typeof(long),
            "real64" or "double" => typeof(double),
            "real32" or "single" => typeof(float),
            "boolean" or "bool" => typeof(bool),
            "datetime" => typeof(DateTime),
            "sint16" or "int16" => typeof(short),
            "uint16" => typeof(ushort),
            "sint8" or "int8" or "uint8" => typeof(byte),
            _ => typeof(string) // Default to string for unknown types
        };
    }

    /// <summary>
    /// Determines if a property has a meaningful value for WMI method parameters.
    /// Also handles converting PropertyGrid's "<null>" representation back to actual null.
    /// </summary>
    private static bool HasMeaningfulValue(PropertyData prop)
    {
        if (prop.Value == null)
            return false;

        // For strings, handle PropertyGrid's "<null>" representation and check for meaningful content
        if (prop.Value is string strValue)
        {
            // Convert PropertyGrid's "<null>" representation back to actual null
            if (strValue.Trim() == "<null>" || strValue.Trim() == "null")
            {
                prop.Value = null;
                Log.Debug("Converted '<null>' back to null for property {PropertyName}", prop.Name);
                return false; // null values are not meaningful for parameters
            }            // Handle comma/semicolon-separated array values if this property is supposed to be an array
            if (prop.IsArray && !string.IsNullOrWhiteSpace(strValue))
            {
                try
                {
                    var parsedArrayValue = ParseStringToArray(strValue, prop.Type.ToString());
                    if (parsedArrayValue != null)
                    {
                        prop.Value = parsedArrayValue;
                        Log.Debug("Converted comma/semicolon-separated string to array for property {PropertyName}", prop.Name);
                        return parsedArrayValue.Length > 0;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to convert string to array for property {PropertyName}: {Value}", prop.Name, strValue);
                }
            }

            return !string.IsNullOrWhiteSpace(strValue);
        }

        // For arrays, check if it has elements
        if (prop.IsArray && prop.Value is Array arrayValue)
        {
            return arrayValue.Length > 0;
        }

        // For other types, any non-null value is meaningful
        return true;
    }

    /// <summary>
    /// Parses a comma/semicolon-separated string into an array based on the property type
    /// </summary>
    private static Array? ParseStringToArray(string value, string? cimType)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(cimType))
            return null;

        // Parse the separators (comma or semicolon)
        var separators = new[] { ',', ';' };
        var stringValues = value.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .ToArray();

        if (stringValues.Length == 0)
            return null;

        try
        {
            // Determine array type based on CIM type
            Type? elementType = GetArrayElementTypeFromCimType(cimType);
            if (elementType == null)
                return null;

            var array = Array.CreateInstance(elementType, stringValues.Length);

            for (int i = 0; i < stringValues.Length; i++)
            {
                object? convertedValue = ConvertStringToType(stringValues[i], elementType);
                array.SetValue(convertedValue, i);
            }

            return array;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse array from string: {Value}", value);
            return null;
        }
    }
}