using System.Collections;
using System.ComponentModel;
using System.Reflection;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.Controls.PropertyGrid.Providers
{
    /// <summary>
    /// Default property provider that handles standard .NET types using reflection.
    /// This provider serves as the fallback for any types not handled by specialized providers.
    /// </summary>
    public class DefaultPropertyTypeProvider : IPropertyTypeProvider
    {
        private const int MaxRecursionDepth = 10;

        private string GetPropertyCategory(PropertyInfo propertyInfo)
        {
            var categoryAttribute = propertyInfo.GetCustomAttribute<CategoryAttribute>();
            return categoryAttribute?.Category ?? "Misc";
        }

        private int GetPropertyOrder(PropertyInfo propertyInfo)
        {
            var orderAttribute = propertyInfo.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
            return orderAttribute?.GetOrder() ?? int.MaxValue;
        }

        /// <summary>
        /// Determines if this provider can handle the specified object type.
        /// </summary>
        public bool CanHandle(Type? objectType)
        {
            // Default provider can handle any type that's not null
            return objectType != null;
        }

        /// <summary>
        /// Gets child items for an expandable property value (collection or complex object).
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetChildItems(object value, string parentName, string parentCategory)
        {
            if (value == null)
                yield break;

            Type valueType = value.GetType();

            // Handle arrays
            if (valueType.IsArray)
            {
                Array array = (Array)value;
                for (int i = 0; i < array.Length; i++)
                {
                    yield return new IndexedPropertyDescriptor(array, i, $"[{i}]", $"[{i}]", parentCategory);
                }
                yield break;
            }

            // Handle dictionaries
            if (value is IDictionary dictionary)
            {
                int index = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    yield return new DictionaryEntryPropertyDescriptor(entry, index, "Key", parentCategory);
                    yield return new DictionaryEntryPropertyDescriptor(entry, index, "Value", parentCategory);
                    index++;
                }
                yield break;
            }

            // Handle other collections
            if (value is ICollection collection && !(value is string))
            {
                int index = 0;
                foreach (var item in collection)
                {
                    yield return new CollectionItemPropertyDescriptor(collection, item, index, parentCategory);
                    index++;
                }
                yield break;
            }

            // Handle IEnumerable that's not a collection or string
            if (value is IEnumerable enumerable && !(value is string) && !(value is ICollection))
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    yield return new CollectionItemPropertyDescriptor(enumerable, item, index, parentCategory);
                    index++;
                }
                yield break;
            }

            // Handle complex objects with properties
            if (!valueType.IsPrimitive &&
                !valueType.IsEnum &&
                valueType != typeof(string) &&
                valueType != typeof(DateTime) &&
                valueType != typeof(decimal) &&
                valueType != typeof(Guid))
            {
                foreach (var property in GetProperties(value))
                {
                    yield return property;
                }
            }
        }

        /// <summary>
        /// Gets all property descriptors for the specified object.
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetProperties(object obj)
        {
            if (obj == null)
                yield break;

            Type type = obj.GetType();

            // Get all public instance properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !Attribute.IsDefined(p, typeof(BrowsableAttribute)) ||
                            (Attribute.IsDefined(p, typeof(BrowsableAttribute)) &&
                             ((BrowsableAttribute?)Attribute.GetCustomAttribute(p, typeof(BrowsableAttribute)))?.Browsable != false))
                .OrderBy(GetPropertyOrder)
                .ThenBy(p => p.Name);

            foreach (var prop in properties)
            {
                // Skip indexers
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                string category = GetPropertyCategory(prop);
                yield return new DefaultPropertyDescriptor(prop, obj, category);
            }
        }

        /// <summary>
        /// Determines if the specified value represents a collection or complex object that can be expanded.
        /// </summary>
        public bool IsExpandable(object value, Type valueType)
        {
            if (value == null)
                return false;

            // Strings are not considered expandable collections
            if (value is string)
                return false;

            // Check if it's an array
            if (valueType.IsArray)
                return true;

            // Check if it's a collection but not a string
            if (value is ICollection)
                return true;

            // Check if it's an enumerable but not a string
            if (valueType != typeof(string) &&
                ((valueType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(valueType)) ||
                (typeof(IEnumerable).IsAssignableFrom(valueType))))
                return true;

            // Check if it's a complex object with properties (not a primitive or simple type)
            if (!valueType.IsPrimitive &&
                !valueType.IsEnum &&
                valueType != typeof(string) &&
                valueType != typeof(DateTime) &&
                valueType != typeof(decimal) &&
                valueType != typeof(Guid))
            {
                // Check if it has public properties
                PropertyInfo[] properties = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                return properties.Length > 0;
            }

            return false;
        }

        #region Specialized Property Descriptors for Collections

        /// <summary>
        /// Property descriptor for collection items
        /// </summary>
        private class CollectionItemPropertyDescriptor : IPropertyDescriptor
        {
            private readonly object _collection;
            private readonly int _index;
            private readonly object _item;

            public CollectionItemPropertyDescriptor(object collection, object item, int index, string category)
            {
                _collection = collection;
                _item = item;
                _index = index;
                Name = $"[{index}]";
                DisplayName = $"[{index}]";
                Category = category;
            }

            public string Category { get; }
            public string Description => $"Collection Item at index {_index}";
            public string DisplayName { get; }
            public bool IsReadOnly => true;
            public string Name { get; }
            public Type? PropertyType => _item?.GetType() ?? typeof(object);

            // Most collections don't support direct item replacement
            public object Source => _collection;

            public object? Value => _item;

            public bool SetValue(object? value)
            {
                // Most collection types don't support direct item replacement
                return false;
            }
        }

        /// <summary>
        /// Property descriptor for dictionary entries
        /// </summary>
        private class DictionaryEntryPropertyDescriptor : IPropertyDescriptor
        {
            private readonly DictionaryEntry _entry;
            private readonly int _index;
            private readonly string _propertyName;

            public DictionaryEntryPropertyDescriptor(DictionaryEntry entry, int index, string propertyName, string category)
            {
                _entry = entry;
                _index = index;
                _propertyName = propertyName;
                Name = $"[{index}].{propertyName}";
                DisplayName = $"{propertyName}";
                Category = category;
            }

            public string Category { get; }
            public string Description => $"Dictionary {_propertyName.ToLowerInvariant()} at index {_index}";
            public string DisplayName { get; }
            public bool IsReadOnly => true;
            public string Name { get; }
            public Type? PropertyType => Value?.GetType() ?? typeof(object);

            // Dictionary entries are typically immutable
            public object Source => _entry;

            public object? Value => _propertyName == "Key" ? _entry.Key : _entry.Value;

            public bool SetValue(object? value)
            {
                // Dictionary entries are typically immutable
                return false;
            }
        }

        /// <summary>
        /// Property descriptor for array and collection indexed items
        /// </summary>
        private class IndexedPropertyDescriptor : IPropertyDescriptor
        {
            private readonly Array _array;
            private readonly int _index;

            public IndexedPropertyDescriptor(Array array, int index, string name, string displayName, string category)
            {
                _array = array;
                _index = index;
                Name = name;
                DisplayName = displayName;
                Category = category;
            }

            public string Category { get; }
            public string Description => $"Item at index {_index}";
            public string DisplayName { get; }
            public bool IsReadOnly => false;
            public string Name { get; }
            public Type? PropertyType => Value?.GetType() ?? _array.GetType().GetElementType();
            public object Source => _array;
            public object? Value => _array.GetValue(_index);

            public bool SetValue(object? value)
            {
                try
                {
                    _array.SetValue(value, _index);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion Specialized Property Descriptors for Collections
    }
}