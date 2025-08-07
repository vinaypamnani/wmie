using System.Management;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property descriptor for WMI qualifier data
/// </summary>
public class WmiQualifierPropertyDescriptor : IPropertyDescriptor
{
    private readonly string _category;
    private readonly QualifierData _qualifier;
    private object _source;

    public WmiQualifierPropertyDescriptor(QualifierData qualifier, string category, object? source = null)
    {
        _qualifier = qualifier;
        _category = category;
        _source = source ?? qualifier;
    }

    public string Category => _category;

    public string Description
    {
        get
        {
            return _qualifier.Value != null ? $"Type: {_qualifier.Value.GetType().Name}" : string.Empty;
        }
    }

    public string DisplayName => _qualifier.Name;
    public bool IsKey => false;
    public bool IsReadOnly => true;
    public string Name => _qualifier.Name;
    public Type? PropertyType => typeof(QualifierData);
    public object Source => _source;
    public object? Value => _qualifier;

    public bool SetValue(object? value)
    {
        // WMI qualifiers are typically not modifiable
        return false;
    }
}