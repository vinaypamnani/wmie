namespace WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions
{
    /// <summary>
    /// Registry for property type providers. Manages the collection of providers
    /// and selects the appropriate provider for each object type.
    /// </summary>
    public class PropertyTypeProviderRegistry
    {
        private readonly List<IPropertyValueConverter> _converters = new List<IPropertyValueConverter>();
        private readonly IPropertyTypeProvider _fallbackProvider;
        private readonly object _lockObject = new object();
        private readonly List<IPropertyTypeProvider> _providers = new List<IPropertyTypeProvider>();

        private PropertyTypeProviderRegistry()
        {
            // Create a fallback provider that returns empty collections and doesn't throw exceptions
            _fallbackProvider = new FallbackPropertyTypeProvider();
        }

        /// <summary>
        /// Singleton instance of the registry
        /// </summary>
        public static PropertyTypeProviderRegistry Instance { get; } = new PropertyTypeProviderRegistry();

        /// <summary>
        /// Creates a formatted string representation of a property value
        /// </summary>
        public string FormatValue(object? value, Type propertyType)
        {
            if (value == null)
                return "<null>";

            var converter = GetConverter(propertyType);
            if (converter != null)
            {
                return converter.ConvertToString(value, propertyType);
            }

            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets property descriptors for the child items of an expandable property
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetChildItems(object? value, string parentName, string parentCategory)
        {
            if (value == null)
                return Enumerable.Empty<IPropertyDescriptor>();

            Type objectType = value.GetType();
            var provider = GetProvider(objectType);
            return provider.GetChildItems(value, parentName, parentCategory);
        }

        /// <summary>
        /// Gets the appropriate value converter for the specified property type
        /// </summary>
        public IPropertyValueConverter? GetConverter(Type propertyType)
        {
            if (propertyType == null)
                throw new ArgumentNullException(nameof(propertyType));

            lock (_lockObject)
            {
                return _converters.FirstOrDefault(c => c.CanConvert(propertyType));
            }
        }

        /// <summary>
        /// Gets property descriptors for all properties of the specified object
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetProperties(object? obj)
        {
            if (obj == null)
                return Enumerable.Empty<IPropertyDescriptor>();

            Type objectType = obj.GetType();
            var provider = GetProvider(objectType);
            return provider.GetProperties(obj);
        }

        /// <summary>
        /// Gets the appropriate provider for the specified object type.
        /// If no suitable provider is found, returns a fallback provider.
        /// </summary>
        public IPropertyTypeProvider GetProvider(Type objectType)
        {
            if (objectType == null)
                throw new ArgumentNullException(nameof(objectType));

            lock (_lockObject)
            {
                return _providers.FirstOrDefault(p => p.CanHandle(objectType)) ?? _fallbackProvider;
            }
        }

        /// <summary>
        /// Determines if the specified value is expandable (collection or complex object)
        /// </summary>
        public bool IsExpandable(object? value, Type valueType)
        {
            if (value == null)
                return false;

            Type objectType = value.GetType();
            var provider = GetProvider(objectType);
            return provider.IsExpandable(value, valueType);
        }

        /// <summary>
        /// Registers a property value converter in a thread-safe manner
        /// </summary>
        public void RegisterConverter(IPropertyValueConverter converter)
        {
            if (converter == null)
                throw new ArgumentNullException(nameof(converter));

            lock (_lockObject)
            {
                if (!_converters.Contains(converter))
                {
                    _converters.Add(converter);
                    // Sort converters by descending priority
                    _converters.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                }
            }
        }

        /// <summary>
        /// Registers a property type provider in a thread-safe manner
        /// </summary>
        public void RegisterProvider(IPropertyTypeProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (_lockObject)
            {
                if (!_providers.Contains(provider))
                {
                    _providers.Add(provider);
                }
            }
        }

        /// <summary>
        /// A simple fallback provider that returns empty collections
        /// and provides sensible defaults for all required operations.
        /// </summary>
        private class FallbackPropertyTypeProvider : IPropertyTypeProvider
        {
            public bool CanHandle(Type objectType) => true;

            public IEnumerable<IPropertyDescriptor> GetChildItems(object obj, string parentName, string parentCategory)
                => Enumerable.Empty<IPropertyDescriptor>();

            public IEnumerable<IPropertyDescriptor> GetProperties(object obj)
                            => Enumerable.Empty<IPropertyDescriptor>();

            public bool IsExpandable(object value, Type valueType) => false;
        }
    }
}