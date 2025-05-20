using System.ComponentModel;
using System.Reflection;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Providers
{
    /// <summary>
    /// Property descriptor implementation that uses reflection to access standard .NET properties.
    /// </summary>
    public class DefaultPropertyDescriptor : IPropertyDescriptor
    {
        private readonly string _category;
        private readonly string _description;
        private readonly bool _isReadOnly;
        private readonly PropertyInfo _propertyInfo;
        private readonly object _source;

        /// <summary>
        /// Creates a new DefaultPropertyDescriptor instance.
        /// </summary>
        public DefaultPropertyDescriptor(PropertyInfo propertyInfo, object source, string category = "Misc")
        {
            _propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _category = category;

            // Determine if the property is read-only
            _isReadOnly = !_propertyInfo.CanWrite;

            // Get the display name from attribute or use property name
            var displayNameAttribute = propertyInfo.GetCustomAttribute<DisplayNameAttribute>();
            DisplayName = displayNameAttribute?.DisplayName ?? propertyInfo.Name;

            // Get the description from attribute
            var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
            string attributeDescription = descriptionAttribute?.Description ?? string.Empty;

            // Format the description according to requirements
            _description = FormatPropertyDescription(attributeDescription);
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
        public string DisplayName { get; }

        /// <summary>
        /// Gets whether the property is read-only.
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name => _propertyInfo.Name;

        /// <summary>
        /// Gets the type of the property.
        /// </summary>
        public Type PropertyType => _propertyInfo.PropertyType;

        /// <summary>
        /// Gets the source object containing this property.
        /// </summary>
        public object Source => _source;

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public object? Value
        {
            get
            {
                try
                {
                    return _propertyInfo.GetValue(_source);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the underlying PropertyInfo for this property.
        /// </summary>
        public PropertyInfo PropertyInfo => _propertyInfo;

        /// <summary>
        /// Formats the property description according to the standard format.
        /// </summary>
        /// <param name="attributeDescription">The original description from attribute if any</param>
        /// <returns>Formatted description</returns>
        private string FormatPropertyDescription(string attributeDescription)
        {
            // Format the description according to requirements for non-WMI types
            string arrayIndicator = PropertyType.IsArray ? " (Array)" : "";
            string typeDescription = $"Type: {PropertyType.Name}{arrayIndicator}";

            // If we have an attribute description, include it after the type information
            if (!string.IsNullOrEmpty(attributeDescription))
            {
                return $"{typeDescription}\n{attributeDescription}";
            }
            else
            {
                return typeDescription;
            }
        }

        /// <summary>
        /// Sets the value of the property if it is writable.
        /// </summary>
        public bool SetValue(object? value)
        {
            if (IsReadOnly)
                return false;

            try
            {
                _propertyInfo.SetValue(_source, value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}