namespace WmiExplorer.PropertyGrid.Abstractions;

/// <summary>
/// Interface for providers that handle specific types of properties.
/// Implementations can handle standard .NET types, WMI types, or custom types.
/// </summary>
public interface IPropertyTypeProvider
{
    bool CanHandle(Type objectType);
    IEnumerable<IPropertyDescriptor> GetChildItems(object value, string parentName, string parentCategory, IPropertyGridContext? propertyGridContext = null);
    IEnumerable<IPropertyDescriptor> GetProperties(object obj, IPropertyGridContext? propertyGridContext = null);
    bool IsExpandable(object value, Type valueType);
}