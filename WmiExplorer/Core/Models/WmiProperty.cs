using System.Collections.Specialized;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Logging;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Thin wrapper for a WMI PropertyData object
/// </summary>
public class WmiProperty
{
    private object? _cachedValueMap;
    private object? _cachedValues;
    private readonly ManagementClass? _parentClass;
    private bool _possibleValuesComputed;
    private readonly PropertyData _propertyData;

    public WmiProperty(PropertyData propertyData, ManagementClass? parentClass = null)
    {
        _propertyData = propertyData ?? throw new ArgumentNullException(nameof(propertyData));
        _parentClass = parentClass;
    }

    [Browsable(false)]
    public PropertyData ActualProperty => _propertyData;

    public string ClassName => _parentClass?["__Class"]?.ToString() ?? string.Empty;
    public string ClassPath => _parentClass?.Path?.Path ?? string.Empty;

    [Browsable(false)]
    public string Description
    {
        get
        {
            try
            {
                if (_propertyData.Qualifiers != null && _propertyData.Qualifiers.Cast<QualifierData>().Any(q => q.Name == "Description"))
                    return _propertyData.Qualifiers["Description"]?.Value?.ToString() ?? string.Empty;
            }
            catch
            {
                // Optionally log the error
            }
            return string.Empty;
        }
    }

    [Category("Property")]
    public bool IsArray => _propertyData.IsArray;

    /// <summary>
    /// Determines if this property is a key property based on the 'key' qualifier
    /// </summary>
    [Category("Property")]
    public bool IsKey
    {
        get
        {
            try
            {
                return _propertyData.Qualifiers?.Cast<QualifierData>()
                    .Any(q => q.Name.Equals("key", StringComparison.OrdinalIgnoreCase)) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Determines if this property is lazy-loaded based on the 'lazy' qualifier
    /// </summary>
    [Category("Property")]
    public bool IsLazy
    {
        get
        {
            try
            {
                return _propertyData.Qualifiers?.Cast<QualifierData>()
                    .Any(q => q.Name.Equals("lazy", StringComparison.OrdinalIgnoreCase)) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    [Category("Property")]
    public string Name => _propertyData.Name;

    [Category("Property")]
    public string Origin => _propertyData.Origin;

    /// <summary>
    /// Gets the possible enumeration values for this property, if available.
    /// </summary>
    [Category("Value Map")]
    [ShowChildrenAsParent]
    public NameValueCollection? PossibleValues
    {
        get
        {
            if (!_possibleValuesComputed)
            {
                BuildAndCachePossibleValues();
                _possibleValuesComputed = true;
            }

            // Always create a fresh NameValueCollection from cached data or else PropertyGrid doesn't update on reselection
            if (_cachedValues != null)
            {
                return CreateNameValueCollection(_cachedValues, _cachedValueMap);
            }

            return null;
        }
    }

    [Category("Qualifiers")]
    [ShowChildrenAsParent]
    public QualifierDataCollection Qualifiers => _propertyData.Qualifiers;

    [Category("Property")]
    public string Type => _propertyData.Type.ToString();

    [Category("Property")]
    public object Value => _propertyData.Value;

    public override string ToString() => $"Property: {Name} ({Type})";

    /// <summary>
    /// Builds and caches the possible values data from WMI qualifiers.
    /// </summary>
    private void BuildAndCachePossibleValues()
    {
        var qualifiers = _propertyData.Qualifiers;
        if (qualifiers == null || qualifiers.Count == 0)
            return;

        // Use a more efficient lookup for qualifiers
        try
        {
            // Look for values qualifiers first
            foreach (QualifierData qualifier in qualifiers)
            {
                var name = qualifier.Name;
                if (string.Equals(name, "values", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "enumeration", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "stringenumeration", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "bitvalues", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "bits", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedValues = qualifier.Value;
                    if (_cachedValueMap != null) break; // Both found, exit early
                }
                else if (string.Equals(name, "valuemap", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "bitmap", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedValueMap = qualifier.Value;
                    if (_cachedValues != null) break; // Both found, exit early
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            Log.Warning(ex, "Error building possible values for property '{Name}' in Class '{ClassName}'", Name, ClassName);
        }
    }

    /// <summary>
    /// Creates a NameValueCollection from the values and optional value map.
    /// </summary>
    private static NameValueCollection? CreateNameValueCollection(object values, object? valueMap)
    {
        var result = new NameValueCollection();

        switch (values)
        {
            case string[] valueArray when valueMap is string[] mapArray && valueArray.Length == mapArray.Length:
                // Paired values and map
                for (int i = 0; i < valueArray.Length; i++)
                {
                    result.Add(valueArray[i], mapArray[i]);
                }
                break;

            case string[] valueArray:
                // Only values available, use value as both name and value
                for (int i = 0; i < valueArray.Length; i++)
                {
                    result.Add(valueArray[i], valueArray[i]);
                }
                break;

            case int[] intArray:
                // Integer values
                for (int i = 0; i < intArray.Length; i++)
                {
                    var value = intArray[i].ToString();
                    result.Add(value, value);
                }
                break;

            case string str when !string.IsNullOrEmpty(str):
                // Comma-separated string
                var splitValues = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var splitValue in splitValues)
                {
                    var value = splitValue.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value, value);
                    }
                }
                break;
        }

        return result.Count > 0 ? result : null;
    }
}