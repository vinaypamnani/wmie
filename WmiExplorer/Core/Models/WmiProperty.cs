using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Thin wrapper for a WMI PropertyData object
/// </summary>
public class WmiProperty
{
    private readonly ManagementClass? _parentClass;
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

    public bool IsArray => _propertyData.IsArray;

    /// <summary>
    /// Determines if this property is a key property based on the 'key' qualifier
    /// </summary>
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

    public string Name => _propertyData.Name;
    public QualifierDataCollection Qualifiers => _propertyData.Qualifiers;
    public string Type => _propertyData.Type.ToString();
    public object Value => _propertyData.Value;

    public override string ToString() => $"{Name} ({Type})";
}