using WmiExplorer.Core.Models;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider;

public class WmiMethodPropertyDescriptor : IPropertyDescriptor
{
    private readonly string _category;
    private readonly WmiMethod _method;

    public WmiMethodPropertyDescriptor(WmiMethod method, string category)
    {
        _method = method;
        _category = category;
    }

    public string Category => _category;
    public string Description => _method.Description;
    public string DisplayName => _method.Name;
    public bool IsReadOnly => true;
    public string Name => _method.Name;
    public Type? PropertyType => typeof(WmiMethod);
    public object Source => _method;

    // Return full object as Value for expansion
    public object? Value => _method;
    public bool SetValue(object? value) => false;
}