using System.Collections.Specialized;
using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Thin wrapper for a WMI PropertyData object
/// </summary>
public class WmiProperty
{
    private NameValueCollection? _cachedPossibleValues;
    private readonly ManagementClass? _parentClass;
    private readonly PropertyData _propertyData;
    private static readonly NameValueCollection EmptyCollection = new();

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
    [Category("Help")]
    public NameValueCollection? PossibleValues
    {
        get
        {
            // Use sentinel pattern: EmptyCollection means "evaluated but no values found"
            if (_cachedPossibleValues == EmptyCollection)
                return null;

            // Return cached result if already evaluated and has values
            if (_cachedPossibleValues != null)
                return _cachedPossibleValues;

            // Evaluate and cache the result
            _cachedPossibleValues = BuildPossibleValues() ?? EmptyCollection;

            // Return null if no values were found (EmptyCollection sentinel)
            return _cachedPossibleValues == EmptyCollection ? null : _cachedPossibleValues;
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
    /// Builds the possible values collection from WMI qualifiers.
    /// </summary>
    private NameValueCollection? BuildPossibleValues()
    {
        var qualifiers = _propertyData.Qualifiers;
        if (qualifiers == null || qualifiers.Count == 0)
            return null;

        object? values = null;
        object? valueMap = null;

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
                    values = qualifier.Value;
                    if (valueMap != null) break; // Both found, exit early
                }
                else if (string.Equals(name, "valuemap", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "bitmap", StringComparison.OrdinalIgnoreCase))
                {
                    valueMap = qualifier.Value;
                    if (values != null) break; // Both found, exit early
                }
            }

            // Early return if no values found
            if (values == null)
                return null;

            return CreateNameValueCollection(values, valueMap);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a NameValueCollection from the values and optional value map.
    /// </summary>
    private static NameValueCollection CreateNameValueCollection(object values, object? valueMap)
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
                    var value = valueArray[i];
                    result.Add(value, value);
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
                for (int i = 0; i < splitValues.Length; i++)
                {
                    var value = splitValues[i].Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value, value);
                    }
                }
                break;
        }

        return result.Count > 0 ? result : null!;
    }
}