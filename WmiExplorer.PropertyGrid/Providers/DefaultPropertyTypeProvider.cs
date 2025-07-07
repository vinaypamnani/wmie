using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Providers;

/// <summary>
/// Default property provider that handles standard .NET types using reflection.
/// This provider serves as the fallback for any types not handled by specialized providers.
/// </summary>
public class DefaultPropertyTypeProvider : IPropertyTypeProvider
{
    private const int MaxRecursionDepth = 10;

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
    public IEnumerable<IPropertyDescriptor> GetChildItems(object value, string parentName, string parentCategory, IPropertyGridContext? propertyGridContext = null)
    {
        if (value == null)
            yield break;

        Type valueType = value.GetType();

        // Handle NameObjectCollectionBase (NameValueCollection, ManagementNamedValueCollection, etc.)
        if (value is NameObjectCollectionBase nameObjectCollection)
        {
            var keys = nameObjectCollection.Keys;
            for (int i = 0; i < nameObjectCollection.Count; i++)
            {
                string? key = keys[i];
                string displayKey = key ?? $"[{i}]";
                yield return new NameObjectCollectionPropertyDescriptor(nameObjectCollection, i, displayKey, parentCategory);
            }
            yield break;
        }

        // Handle array of KeyValuePair<string, object> (for PossibleValues)
        if (valueType.IsArray)
        {
            var elementType = valueType.GetElementType();
            if (elementType != null && elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProp = elementType.GetProperty("Key");
                var valueProp = elementType.GetProperty("Value");
                var kvpArray = (Array)value;
                var descriptors = new List<KeyValuePairPropertyDescriptor>();
                for (int i = 0; i < kvpArray.Length; i++)
                {
                    var item = kvpArray.GetValue(i);
                    if (item != null && keyProp != null && valueProp != null)
                    {
                        var key = keyProp.GetValue(item)?.ToString() ?? $"[{i}]";
                        var val = valueProp.GetValue(item);
                        descriptors.Add(new KeyValuePairPropertyDescriptor(key, val, parentCategory));
                    }
                }
                foreach (var desc in descriptors)
                {
                    yield return desc;
                }
                yield break;
            }

            // Special handling for string arrays with pipe-separated values
            var array = (Array)value;
            if (array.GetType().GetElementType() == typeof(string) && array.Length == 1)
            {
                var singleValue = array.GetValue(0) as string;
                if (!string.IsNullOrEmpty(singleValue) && singleValue.Contains('|'))
                {
                    var splitValues = singleValue.Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim()).ToArray();
                    for (int i = 0; i < splitValues.Length; i++)
                    {
                        yield return new PipeSeparatedValueDescriptor(splitValues, i, $"[{i}]", $"[{i}]", parentCategory);
                    }
                    yield break;
                }
            }

            // Default array handling
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
            foreach (var property in GetProperties(value, propertyGridContext))
            {
                yield return property;
            }
        }
    }

    /// <summary>
    /// Gets all property descriptors for the specified object.
    /// </summary>
    public IEnumerable<IPropertyDescriptor> GetProperties(object obj, IPropertyGridContext? propertyGridContext = null)
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
            yield return (Abstractions.IPropertyDescriptor)new DefaultPropertyDescriptor(prop, obj, category);
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
    /// Property descriptor for collection items
    /// </summary>
    internal class CollectionItemPropertyDescriptor : IPropertyDescriptor
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
        public bool IsKey => false;
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
    internal class DictionaryEntryPropertyDescriptor : IPropertyDescriptor
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
        public bool IsKey => false;
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
    internal class IndexedPropertyDescriptor : IPropertyDescriptor
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
        public bool IsKey => false;
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

    /// <summary>
    /// Property descriptor for KeyValuePair (used for PossibleValues)
    /// </summary>
    internal class KeyValuePairPropertyDescriptor : IPropertyDescriptor
    {
        public KeyValuePairPropertyDescriptor(string key, object? value, string category)
        {
            Name = key;
            DisplayName = key;
            Category = category;
            Value = value;
        }

        public string Category { get; }
        public string Description => $"Key-value pair: {Name}";
        public string DisplayName { get; }
        public bool IsKey => false;
        public bool IsReadOnly => true;
        public string Name { get; }
        public Type? PropertyType => Value?.GetType() ?? typeof(object);
        public object Source => this;
        public object? Value { get; }

        public bool SetValue(object? value) => false;
    }

    /// <summary>
    /// Property descriptor for NameObjectCollectionBase entries (ManagementNamedValueCollection, NameValueCollection, etc.)
    /// </summary>
    internal class NameObjectCollectionPropertyDescriptor : IPropertyDescriptor
    {
        private readonly NameObjectCollectionBase _collection;
        private readonly int _index;
        private readonly string _key;

        public NameObjectCollectionPropertyDescriptor(NameObjectCollectionBase collection, int index, string key, string category)
        {
            _collection = collection;
            _index = index;
            _key = key;
            Name = key;
            DisplayName = key;
            Category = category;
        }

        public string Category { get; }
        public string Description => $"Name-value pair: '{_key}'";
        public string DisplayName { get; }
        public bool IsKey => false;
        public bool IsReadOnly => true;

        // Most NameObjectCollectionBase implementations are read-only through indexer
        public string Name { get; }

        public Type? PropertyType => Value?.GetType() ?? typeof(object);
        public object Source => _collection;

        public object? Value
        {
            get
            {
                try
                {
                    // For NameValueCollection, use the string indexer to get the first value
                    if (_collection is NameValueCollection nvc)
                    {
                        return nvc[_key];
                    }

                    // Use reflection to access the protected BaseGet method
                    var baseGetMethod = typeof(NameObjectCollectionBase).GetMethod("BaseGet",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(int) },
                        null);
                    var result = baseGetMethod?.Invoke(_collection, new object[] { _index });

                    // If result is an array with one element, return just that element
                    if (result is Array arr && arr.Length == 1)
                    {
                        return arr.GetValue(0);
                    }

                    return result;
                }
                catch
                {
                    // Fallback: try to access through Keys and then indexer if available
                    try
                    {
                        // For derived types like NameValueCollection, try indexer access
                        var indexerProperty = _collection.GetType().GetProperty("Item", new[] { typeof(string) });
                        if (indexerProperty != null)
                        {
                            return indexerProperty.GetValue(_collection, new object[] { _key });
                        }
                    }
                    catch
                    {
                        // Ignore and return null
                    }
                    return null;
                }
            }
        }

        public bool SetValue(object? value)
        {
            try
            {
                // Try to use BaseSet method if available
                var baseSetMethod = typeof(NameObjectCollectionBase).GetMethod("BaseSet",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(object) },
                    null); if (baseSetMethod != null)
                {
                    baseSetMethod.Invoke(_collection, new object?[] { _key, value });
                    return true;
                }

                // Fallback: try indexer setter if available
                var indexerProperty = _collection.GetType().GetProperty("Item", new[] { typeof(string) });
                if (indexerProperty != null && indexerProperty.CanWrite)
                {
                    indexerProperty.SetValue(_collection, value, new object[] { _key });
                    return true;
                }
            }
            catch
            {
                // Setting failed
            }
            return false;
        }
    }

    /// <summary>
    /// Property descriptor for pipe-separated values within a single string array element
    /// </summary>
    internal class PipeSeparatedValueDescriptor : IPropertyDescriptor
    {
        private readonly int _index;
        private readonly string[] _values;

        public PipeSeparatedValueDescriptor(string[] values, int index, string name, string displayName, string category)
        {
            _values = values;
            _index = index;
            Name = name;
            DisplayName = displayName;
            Category = category;
        }

        public string Category { get; }
        public string Description => $"Pipe-separated value at index {_index}";
        public string DisplayName { get; }
        public bool IsKey => false;
        public bool IsReadOnly => true;

        // Pipe-separated values are typically read-only
        public string Name { get; }

        public Type? PropertyType => typeof(string);
        public object Source => _values;
        public object? Value => _index < _values.Length ? _values[_index] : null;

        public bool SetValue(object? value)
        {
            // Pipe-separated values are typically read-only in WMI contexts
            return false;
        }
    }
}