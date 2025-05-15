using System.Management;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    /// <summary>
    /// Property descriptor for WMI qualifier data
    /// </summary>
    public class WmiQualifierPropertyDescriptor : IPropertyDescriptor
    {
        private readonly string _category;
        private readonly QualifierData _qualifier;

        public WmiQualifierPropertyDescriptor(QualifierData qualifier, string category)
        {
            _qualifier = qualifier;
            _category = category;
        }

        public string Category => _category;
        public string Description => _qualifier.Value != null ? $"Type: {_qualifier.Value.GetType().Name}" : string.Empty;
        public string DisplayName => _qualifier.Name;
        public bool IsReadOnly => true;
        public string Name => _qualifier.Name;
        public Type? PropertyType => Value?.GetType() ?? typeof(object);
        public object Source => _qualifier;
        public object? Value => _qualifier.Value;

        public bool SetValue(object? value)
        {
            // WMI qualifiers are typically not modifiable
            return false;
        }
    }
}