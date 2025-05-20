using WmiExplorer.Core.Models;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    public class WmiMethodPropertyDescriptor : IPropertyDescriptor
    {
        private readonly WmiMethod _method;
        private readonly string _category;

        public WmiMethodPropertyDescriptor(WmiMethod method, string category)
        {
            _method = method;
            _category = category;
        }

        public string Name => _method.Name;
        public string DisplayName => _method.Name;
        public string Category => _category;
        public string Description => _method.Description;
        public bool IsReadOnly => true;
        public Type? PropertyType => typeof(WmiMethod);
        public object Source => _method;
        public object? Value => _method; // Return full object for expansion
        public bool SetValue(object? value) => false;
    }
}
