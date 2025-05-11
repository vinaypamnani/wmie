using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.Controls.PropertyGrid.Providers
{
    /// <summary>
    /// Property descriptor for synthetic properties that don't come directly from PropertyInfo or WMI PropertyData.
    /// Used to create virtual properties for metadata like Qualifiers, SystemProperties, etc.
    /// </summary>
    public class SyntheticPropertyDescriptor : IPropertyDescriptor
    {
        private readonly string _category;
        private readonly string _description;
        private readonly string _displayName;
        private readonly bool _isReadOnly;
        private readonly string _name;
        private readonly Type _propertyType;
        private readonly object _source;
        private readonly object _value;

        /// <summary>
        /// Creates a new SyntheticPropertyDescriptor instance.
        /// </summary>
        public SyntheticPropertyDescriptor(
            string name,
            string displayName,
            object value,
            Type propertyType,
            string category,
            bool isReadOnly,
            string description,
            object source)
        {
            _name = name;
            _displayName = displayName;
            _value = value;
            _propertyType = propertyType;
            _category = category;
            _isReadOnly = isReadOnly;
            _description = description;
            _source = source;
        }

        /// <summary>
        /// Gets the category of the property.
        /// </summary>
        public string Category => _category;

        /// <summary>
        /// Gets the description of the property.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets the display name of the property.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets whether the property is read-only.
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the type of the property.
        /// </summary>
        public Type PropertyType => _propertyType;

        /// <summary>
        /// Gets the source object containing this property.
        /// </summary>
        public object Source => _source;

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public object Value => _value;

        /// <summary>
        /// Sets the value of the property if it is writable.
        /// Synthetic properties are typically read-only.
        /// </summary>
        public bool SetValue(object? value)
        {
            // Most synthetic properties are read-only in this context
            return false;
        }
    }
}