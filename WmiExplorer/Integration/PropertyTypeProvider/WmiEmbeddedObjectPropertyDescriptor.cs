using System.Management;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property descriptor for a single embedded WMI object (ManagementBaseObject).
/// </summary>
public class WmiEmbeddedObjectPropertyDescriptor : IPropertyDescriptor
{
    private readonly string _category;
    private readonly string _displayName;
    private readonly ManagementBaseObject _embeddedObject;
    private readonly object _source;

    public WmiEmbeddedObjectPropertyDescriptor(string displayName, ManagementBaseObject embeddedObject, object source, string category)
    {
        _displayName = displayName;
        _embeddedObject = embeddedObject;
        _source = source;
        _category = category;
    }

    public string Category => _category;
    public string Description => $"Embedded WMI object: {_displayName} (Class: {_embeddedObject.ClassPath?.ClassName})";
    public string DisplayName => $"{_embeddedObject.ClassPath?.ClassName} {_displayName}";
    public bool IsKey => false;
    public bool IsReadOnly => true;
    public string Name => _displayName;
    public Type? PropertyType => typeof(ManagementBaseObject);
    public object Source => _source;
    public object? Value => _embeddedObject;

    public bool SetValue(object? value) => false;

    public override string ToString()
    {
        return $"Embedded WMI object: {_displayName} (Class: {_embeddedObject.ClassPath?.ClassName})";
    }
}