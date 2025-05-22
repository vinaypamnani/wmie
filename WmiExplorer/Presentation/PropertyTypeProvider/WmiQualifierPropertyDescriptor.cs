using System.Management;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider;

/// <summary>
/// Property descriptor for WMI qualifier data
/// </summary>
public class WmiQualifierPropertyDescriptor : IPropertyDescriptor
{
    private readonly string _category;
    private readonly string? _providerClsid;
    private readonly QualifierData _qualifier;

    public WmiQualifierPropertyDescriptor(QualifierData qualifier, string category, string? providerClsid = null)
    {
        _qualifier = qualifier;
        _category = category;
        _providerClsid = providerClsid;
    }

    public string Category => _category;

    public string Description
    {
        get
        {
            var desc = _qualifier.Value != null ? $"Type: {_qualifier.Value.GetType().Name}" : string.Empty;
            if (_providerClsid != null)
            {
                desc += $"; CLSID from __Win32Provider: {_providerClsid}";
            }
            return desc;
        }
    }

    public string DisplayName => _qualifier.Name;
    public bool IsReadOnly => true;
    public string Name => _qualifier.Name;
    public Type? PropertyType => typeof(QualifierData);

    // Return the QualifierData object itself
    public string? ProviderClsid => _providerClsid;

    // Mark as expandable
    public object Source => _qualifier;

    public object? Value => _qualifier;

    public bool SetValue(object? value)
    {
        // WMI qualifiers are typically not modifiable
        return false;
    }
}