using System.Collections.Specialized;
using System.ComponentModel;
using System.Management;
using WmiExplorer.Common.Helpers;
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

    [Category("Property")]
    public string CimType => GetQualifierFromClassOrInstance(_propertyData, _parentClass, "cimtype")?.ToString() ?? _propertyData.Type.ToString();

    public string ClassName => _parentClass?["__Class"]?.ToString() ?? string.Empty;
    public string ClassPath => _parentClass?.Path?.Path ?? string.Empty;

    [Browsable(false)]
    public string Description
    {
        get
        {
            var desc = GetQualifierFromClassOrInstance(_propertyData, _parentClass, "Description");
            return desc?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Indicates whether this property has possible values (enumeration or value map).
    /// </summary>
    [Category("Advanced")]
    public bool HasValueMap
    {
        get
        {
            if (!_possibleValuesComputed)
            {
                BuildAndCachePossibleValues();
                _possibleValuesComputed = true;
            }
            return _cachedValues != null;
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
    [Category("Advanced")]
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

    /// <summary>
    /// Indicates whether this property is read-only, based on WMI qualifiers.
    /// </summary>

    [Category("Advanced")]
    public bool IsReadOnly
    {
        get
        {
            // Use GetQualifierFromClassOrInstance for all qualifier checks
            // var isKey = GetQualifierFromClassOrInstance(_propertyData, _parentClass, "key") is bool keyBool && keyBool;
            // if (isKey)
            //     return true; // keys are always read-only

            var writeQualifier = GetQualifierFromClassOrInstance(_propertyData, _parentClass, "write");
            if (writeQualifier is bool writeBool)
                return !writeBool;

            var readQualifier = GetQualifierFromClassOrInstance(_propertyData, _parentClass, "read");
            if (readQualifier is bool readBool && readBool)
                return true;

            var isDynamic = GetQualifierFromClass(_parentClass, "dynamic") is bool dynamicBool && dynamicBool;
            var hasProvider = GetQualifierFromClass(_parentClass, "provider") != null;

            // Special case for dynamic/provider-backed properties with Provider
            if (isDynamic && hasProvider)
                return false; // treat as writable

            // Default: read-only
            return true;
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

            if (_cachedValues != null)
            {
                return ValueMapHelper.CreateNameValueCollection(_cachedValueMap, _cachedValues);
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

    /// <summary>
    /// Gets all qualifiers for the WMI class itself (not property qualifiers).
    /// </summary>
    /// <param name="parentClass">The ManagementClass instance.</param>
    /// <returns>A dictionary of qualifier names and their values, or an empty dictionary if none.</returns>
    public static object? GetQualifierFromClass(ManagementClass? parentClass, string qualifierName)
    {
        if (parentClass == null)
            return null;

        // Check qualifier in the current class
        try
        {
            // Check if the class has qualifiers in the try block to avoid not found exception
            if (parentClass.Qualifiers == null)
                return null;

            foreach (QualifierData qualifier in parentClass.Qualifiers)
            {
                if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
                {
                    return qualifier.Value;
                }
            }
        }
        catch
        {
            // Ignore errors, just return null
        }

        // Walk through derivation chain if present
        try
        {
            var derivationObj = parentClass["Derivation"];
            if (derivationObj is string[] derivationArray && derivationArray.Length > 0)
            {
                foreach (var className in derivationArray)
                {
                    try
                    {
                        // Use parentClass.Scope to preserve connection/auth context
                        var objectOptions = new ObjectGetOptions(null, TimeSpan.FromSeconds(30), true);
                        var derivedClass = new ManagementClass(parentClass.Scope, new ManagementPath(className), objectOptions);
                        if (derivedClass.Qualifiers != null)
                        {
                            foreach (QualifierData qualifier in derivedClass.Qualifiers)
                            {
                                if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return qualifier.Value;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors for individual derived classes
                    }
                }
            }
        }
        catch
        {
            // Ignore errors, just fallback
        }
        return null;
    }

    public override string ToString() => $"Property: {Name} ({Type})";

    /// <summary>
    /// Builds and caches the possible values data from WMI qualifiers.
    /// </summary>
    private void BuildAndCachePossibleValues()
    {
        // Try instance qualifiers first
        ValueMapHelper.GetPossibleValuesAndMap(_propertyData.Qualifiers, out _cachedValues, out _cachedValueMap);
        if (_cachedValues == null && _parentClass != null)
        {
            // Fallback to class property qualifiers
            try
            {
                var classProperty = _parentClass.Properties[_propertyData.Name];
                if (classProperty != null)
                {
                    ValueMapHelper.GetPossibleValuesAndMap(classProperty.Qualifiers, out _cachedValues, out _cachedValueMap);
                }
            }
            catch
            {
                // Ignore errors, just fallback
            }
        }
    }

    /// <summary>
    /// Helper to get a qualifier value from the class definition using UseAmendedQualifiers.
    /// </summary>
    private static object? GetPropertyQualifierFromClass(PropertyData propertyData, ManagementClass? parentClass, string qualifierName)
    {
        if (parentClass == null)
            return null;

        try
        {
            var classProperty = parentClass.Properties[propertyData.Name];
            if (classProperty != null && classProperty.Qualifiers != null)
            {
                foreach (QualifierData qualifier in classProperty.Qualifiers)
                {
                    if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
                    {
                        return qualifier.Value;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors, just fallback
        }

        // Walk through derivation chain if present
        try
        {
            var derivationClasses = parentClass.Derivation;
            if (derivationClasses != null && derivationClasses.Count > 0)
            {
                foreach (var className in derivationClasses)
                {
                    try
                    {
                        var objectOptions = new ObjectGetOptions(null, TimeSpan.FromSeconds(30), true);
                        var derivedClass = new ManagementClass(parentClass.Scope, new ManagementPath(className), objectOptions)
                        {
                            Options = { UseAmendedQualifiers = true }
                        };
                        var derivedProperty = derivedClass.Properties[propertyData.Name];
                        if (derivedProperty != null && derivedProperty.Qualifiers != null)
                        {
                            foreach (QualifierData qualifier in derivedProperty.Qualifiers)
                            {
                                if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return qualifier.Value;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors for individual derived classes
                    }
                }
            }
        }
        catch
        {
            // Ignore errors, just fallback
        }
        return null;
    }

    /// <summary>
    /// Helper to get a qualifier value from the instance, then fall back to the class definition.
    /// </summary>
    private static object? GetQualifierFromClassOrInstance(PropertyData propertyData, ManagementClass? parentClass, string qualifierName)
    {
        var instanceValue = GetQualifierValue(propertyData, qualifierName);
        if (instanceValue != null)
            return instanceValue;

        var classValue = GetPropertyQualifierFromClass(propertyData, parentClass, qualifierName);
        if (classValue != null)
            return classValue;

        return null;
    }

    /// <summary>
    /// Helper method to get a qualifier value by name from specified PropertyData
    /// </summary>
    private static object? GetQualifierValue(PropertyData propertyData, string qualifierName)
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
}