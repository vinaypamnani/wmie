using System.Management;
using WmiExplorer.PropertyGrid.Abstractions;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Collections.Generic;
using System.Linq;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Converter for WMI-specific value formatting and editing.
/// </summary>
public class WmiPropertyValueConverter : IPropertyValueConverter
{
    /// <summary>
    /// Gets the priority of this converter.
    /// </summary>
    public int Priority => 100;

    /// <summary>
    /// Determines
    /// <summary>
    /// Determines if this converter can handle the specified property type.
    /// </summary>

    // Higher priority than default converter

    public bool CanConvert(Type? propertyType)
    {
        if (propertyType == null)
            return false;

        // Check if this is a WMI type
        return typeof(ManagementObject).IsAssignableFrom(propertyType) ||
               typeof(ManagementObject[]).IsAssignableFrom(propertyType) ||
               typeof(ManagementBaseObject).IsAssignableFrom(propertyType) ||
               typeof(ManagementBaseObject[]).IsAssignableFrom(propertyType) ||
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

        // Explicit handling for ManagementObject
        if (value is System.Management.ManagementObject mo)
        {
            // You can customize this string as needed
            return GetEmbeddedObjectDisplayString(mo);
        }

        // Explicit handling for ManagementObject[]
        if (value is System.Management.ManagementObject[] moArray)
        {
            return $"[EmbeddedObject Array: {moArray.Length} object(s)]";
        }

        if (value is ManagementBaseObject mbo)
        {
            return GetEmbeddedObjectDisplayString(mbo);
        }

        if (value is ManagementBaseObject[] mboArray)
        {
            return $"[EmbeddedObject Array: {mboArray.Length} object(s)]";
        }

        if (value is PropertyData propertyData)
        {
            if (propertyData.IsArray && propertyData.Value is Array pdArray)
                return $"{propertyData.Type} Array[{pdArray.Length}]";

            if (propertyData.Value != null)
                return propertyData.Value.ToString() ?? "<null value>";

            return "PropertyData";
        }

        if (value is QualifierData qualifierData)
        {
            if (qualifierData.Value != null)
            {
                // Handle array values in qualifiers properly
                if (qualifierData.Value is Array qualArray && !(qualifierData.Value is string))
                {
                    // For array qualifiers, join the elements with a suitable separator
                    var elements = qualArray.Cast<object>().Select(elem => elem?.ToString() ?? "<null>");
                    return string.Join(", ", elements);
                }
                return qualifierData.Value?.ToString() ?? "<null value>";
            }

            return "QualifierData";
        }

        // Default to class name for other types
        return value.GetType().Name;
    }

    /// <summary>
    /// Gets a value indicating whether the specified property type should be edited with a custom editor.
    /// </summary>
    public bool RequiresCustomEditor(Type? propertyType)
    {
        // WMI types generally can't be edited directly
        return false;
    }

    private string GetEmbeddedObjectDisplayString(ManagementBaseObject mbo)
    {
        // 1. Try instance path (for ManagementObject)
        try
        {
            if (mbo is System.Management.ManagementObject mo && mo.Path != null)
            {
                var relPath = mo.Path.RelativePath;
                if (!string.IsNullOrEmpty(relPath))
                {
                    return $"[Embedded: {relPath}]";
                }
            }
        }
        catch { /* ignore */ }

        // 2. Try key property string using ManagementClass and WmiProperty
        try
        {
            var className = mbo.ClassPath?.ClassName ?? mbo.GetType().Name;
            ManagementClass? mgmtClass = null;
            try
            {
                if (!string.IsNullOrEmpty(className))
                {
                    // Try to get scope from mbo if possible
                    ManagementScope? scope = null;
                    if (mbo is System.Management.ManagementObject mo2)
                        scope = mo2.Scope;
                    mgmtClass = scope != null ? new ManagementClass(scope, new ManagementPath(className), null) : new ManagementClass(className);
                }
            }
            catch { /* ignore */ }
            var keyProps = new List<string>();
            foreach (PropertyData prop in mbo.Properties)
            {
                try
                {
                    var wmiProp = new WmiExplorer.Core.Models.WmiProperty(prop, mgmtClass);
                    if (wmiProp.IsKey)
                    {
                        keyProps.Add($"{prop.Name}={prop.Value}");
                    }
                }
                catch { /* ignore */ }
            }
            if (keyProps.Count > 0)
            {
                var keyString = string.Join(", ", keyProps);
                return $"[Embedded: {className} ({keyString})]";
            }
        }
        catch { /* ignore */ }

        // 3. Try class path
        try
        {
            if (mbo.ClassPath != null)
            {
                var relPath = mbo.ClassPath.RelativePath;
                if (!string.IsNullOrEmpty(relPath))
                {
                    string[] extraPropNames = { "Name", "Id", "DisplayName", "Caption", "Value" };
                    string? extra = null;
                    foreach (var propName in extraPropNames)
                    {
                        var prop = mbo.Properties.Cast<PropertyData>().FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));
                        if (prop != null && prop.Value != null)
                        {
                            extra = $"{propName}={prop.Value}";
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(extra))
                        return $"[Embedded: {relPath} ({extra})]";
                    return $"[Embedded: {relPath}]";
                }
            }
        }
        catch { /* ignore */ }

        // 4. Fallback to ToString
        var fallback = mbo.ToString();
        if (string.IsNullOrEmpty(fallback) || fallback == "{DependencyProperty.UnsetValue}")
            return "<unset>";
        return fallback;
    }
}

public class WmiPropertyValueConverterBinding : IValueConverter
{
    private static readonly WmiPropertyValueConverter _converter = new WmiPropertyValueConverter();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return _converter.ConvertToString(value, value?.GetType() ?? typeof(object));
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}