using System.Collections;
using System.Management;
using System.Reflection;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.Controls.PropertyGrid.Providers
{
    /// <summary>
    /// Property provider that handles WMI-specific types like ManagementBaseObject and related WMI classes.
    /// </summary>
    public class BaseWmiPropertyTypeProvider : IPropertyTypeProvider
    {
        private const int MaxRecursionDepth = 10;

        // Collection of types this provider can handle
        private static readonly HashSet<Type> _supportedTypes = new HashSet<Type>
        {
            typeof(ManagementBaseObject),
            typeof(PropertyData),
            typeof(PropertyDataCollection),
            typeof(QualifierData),
            typeof(QualifierDataCollection),
            typeof(ManagementPath),
            typeof(ManagementScope),
            typeof(ObjectGetOptions),
            typeof(ConnectionOptions)
        };

        // Category mapping for WMI types
        private static readonly Dictionary<Type, string> _typeCategories = new Dictionary<Type, string>
        {
            { typeof(ManagementPath), "Path" },
            { typeof(ManagementScope), "Scope" },
            { typeof(ObjectGetOptions), "Options" },
            { typeof(ConnectionOptions), "Connection" }
        };

        // Use ThreadLocal to ensure thread safety for recursion depth tracking
        private readonly ThreadLocal<int> _recursionDepth = new ThreadLocal<int>(() => 0);

        /// <summary>
        /// Creates array item descriptors for arrays in WMI properties
        /// </summary>
        private static IEnumerable<IPropertyDescriptor> CreateArrayDescriptors(Array array, string category)
        {
            for (int i = 0; i < array.Length; i++)
            {
                yield return new ArrayPropertyDescriptor(array, i, $"[{i}]", $"[{i}]", category);
            }
        }

        /// <summary>
        /// Gets properties using reflection for regular .NET objects
        /// </summary>
        private static IEnumerable<PropertyInfo> GetTypeProperties(object obj)
        {
            return obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        }

        /// <summary>
        /// Creates an error descriptor when an exception occurs
        /// </summary>
        private SyntheticPropertyDescriptor CreateErrorDescriptor(
            string errorMessage,
            string category,
            string description,
            object source)
        {
            return new SyntheticPropertyDescriptor(
                "Error",
                "Error",
                errorMessage,
                typeof(string),
                category,
                false,
                description,
                source);
        }

        /// <summary>
        /// Creates a metadata property descriptor
        /// </summary>
        private SyntheticPropertyDescriptor CreateMetadataProperty(
            string name,
            object value,
            Type type,
            string description,
            object source,
            string category = "Metadata")
        {
            return new SyntheticPropertyDescriptor(
                name,
                name,
                value,
                type,
                category,
                true,
                description,
                source);
        }

        /// <summary>
        /// Creates a property descriptor for PropertyData
        /// </summary>
        private IPropertyDescriptor CreatePropertyDataDescriptor(PropertyData property, string category)
        {
            // Create a dummy ManagementBaseObject to avoid null reference warnings
            var dummySource = new ManagementClass();
            return new BaseWmiPropertyDescriptor(property, dummySource);
        }

        /// <summary>
        /// Creates a property descriptor for QualifierData
        /// </summary>
        private IPropertyDescriptor CreateQualifierDescriptor(QualifierData qualifier, string category)
        {
            return new QualifierPropertyDescriptor(qualifier, category);
        }

        /// <summary>
        /// Creates a property descriptor for System Properties
        /// </summary>
        private IPropertyDescriptor CreateSystemPropertyDescriptor(PropertyData property)
        {
            // Create a dummy ManagementBaseObject to avoid null reference warnings
            var dummySource = new ManagementClass();
            var descriptor = new BaseWmiPropertyDescriptor(property, dummySource)
            {
                Category = "System Properties"
            };
            return descriptor;
        }

        /// <summary>
        /// Gets ManagementClass-specific properties
        /// </summary>
        private IEnumerable<IPropertyDescriptor> GetManagementClassProperties(ManagementClass mc)
        {
            // ClassPath
            if (mc.ClassPath != null)
            {
                yield return CreateMetadataProperty(
                    "ClassPath",
                    mc.ClassPath,
                    typeof(ManagementPath),
                    "Class path for this WMI class",
                    mc);
            }

            // Scope
            if (mc.Scope != null)
            {
                yield return CreateMetadataProperty(
                    "Scope",
                    mc.Scope,
                    typeof(ManagementScope),
                    "Scope (namespace and connection) information for this WMI class",
                    mc);
            }

            // Methods collection
            if (mc.Methods != null && mc.Methods.Count > 0)
            {
                yield return CreateMetadataProperty(
                    "Methods",
                    mc.Methods,
                    mc.Methods.GetType(),
                    "Methods defined in this WMI class",
                    mc,
                    "Class");
            }

            // Derivation hierarchy
            if (mc.Derivation != null && mc.Derivation.Count > 0)
            {
                yield return CreateMetadataProperty(
                    "Derivation",
                    mc.Derivation,
                    mc.Derivation.GetType(),
                    "Class inheritance hierarchy",
                    mc,
                    "Class");
            }
        }

        /// <summary>
        /// Gets ManagementObject-specific properties
        /// </summary>
        private IEnumerable<IPropertyDescriptor> GetManagementObjectInstanceProperties(ManagementObject mo)
        {
            // Path
            if (mo.Path != null)
            {
                yield return CreateMetadataProperty(
                    "Path",
                    mo.Path,
                    typeof(ManagementPath),
                    "Path information for this WMI object",
                    mo);
            }

            // Scope
            if (mo.Scope != null)
            {
                yield return CreateMetadataProperty(
                    "Scope",
                    mo.Scope,
                    typeof(ManagementScope),
                    "Scope (namespace and connection) information for this WMI object",
                    mo);
            }

            // Options
            if (mo.Options != null)
            {
                yield return CreateMetadataProperty(
                    "Options",
                    mo.Options,
                    typeof(ObjectGetOptions),
                    "Object retrieval options for this WMI object",
                    mo);
            }
        }

        /// <summary>
        /// Gets properties for ManagementBaseObject - handles both ManagementClass and ManagementObject
        /// </summary>
        private IEnumerable<IPropertyDescriptor> GetManagementObjectProperties(ManagementBaseObject mbo)
        {
            // Properties from the Properties collection
            if (mbo.Properties != null)
            {
                foreach (PropertyData property in mbo.Properties)
                {
                    yield return new BaseWmiPropertyDescriptor(property, mbo);
                }
            }

            // System properties
            if (mbo.SystemProperties != null)
            {
                foreach (PropertyData property in mbo.SystemProperties)
                {
                    yield return new BaseWmiPropertyDescriptor(property, mbo)
                    {
                        Category = "System Properties"
                    };
                }
            }

            // Add metadata properties

            // 1. Qualifiers collection
            if (mbo.Qualifiers != null && mbo.Qualifiers.Count > 0)
            {
                yield return CreateMetadataProperty(
                    "Qualifiers",
                    mbo.Qualifiers,
                    typeof(QualifierDataCollection),
                    "Qualifier collection for this WMI object",
                    mbo);
            }

            // Handle type-specific properties
            switch (mbo)
            {
                case ManagementClass mc:
                    // Add class-specific properties
                    foreach (var descriptor in GetManagementClassProperties(mc))
                        yield return descriptor;
                    break;

                case ManagementObject mo:
                    // Add instance-specific properties
                    foreach (var descriptor in GetManagementObjectInstanceProperties(mo))
                        yield return descriptor;
                    break;
            }
        }

        /// <summary>
        /// Gets the category for a WMI type
        /// </summary>
        private string GetWmiCategoryForType(Type? type)
        {
            if (type == null)
                return "WMI";

            return _typeCategories.TryGetValue(type, out var category) ? category : "WMI";
        }

        /// <summary>
        /// Logs an error to the debug console
        /// </summary>
        private void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"BaseWmiPropertyTypeProvider Error: {message}");
        }

        /// <summary>
        /// Generic method to process WMI collections with error handling
        /// </summary>
        private IEnumerable<IPropertyDescriptor> ProcessWmiCollection<T>(
            ICollection collection,
            string category,
            Func<T, string, IPropertyDescriptor> createDescriptor)
        {
            if (collection == null)
                yield break;

            List<T>? items = null;
            string? errorMessage = null;

            try
            {
                // Safely extract all items to a list first
                items = new List<T>();
                foreach (T item in collection)
                {
                    if (item != null)
                        items.Add(item);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                LogError($"Error processing WMI collection of type {typeof(T).Name}: {ex.Message}");
            }

            // If we had an error, yield the error property
            if (errorMessage != null)
            {
                yield return CreateErrorDescriptor(
                    $"Error accessing {typeof(T).Name} collection: {errorMessage}",
                    category,
                    "An error occurred while accessing the collection",
                    collection);
            }
            // Otherwise yield all the items from our safely extracted list
            else if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        yield return createDescriptor(item, category);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if this provider can handle the specified object type.
        /// </summary>
        public bool CanHandle(Type? objectType)
        {
            if (objectType == null)
                return false;

            return _supportedTypes.Any(supportedType => supportedType.IsAssignableFrom(objectType));
        }

        /// <summary>
        /// Gets child items for an expandable property value (collection or complex object).
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetChildItems(object? value, string parentName, string parentCategory)
        {
            if (value == null)
                yield break;

            using var recursionScope = new RecursionScope(_recursionDepth);
            if (recursionScope.CurrentDepth > MaxRecursionDepth)
                yield break;

            IEnumerable<IPropertyDescriptor> descriptors = Enumerable.Empty<IPropertyDescriptor>();

            switch (value)
            {
                case ManagementBaseObject mbo:
                    descriptors = GetManagementObjectProperties(mbo);
                    break;

                case PropertyData propertyData when propertyData.IsArray && propertyData.Value is Array pdArray:
                    descriptors = CreateArrayDescriptors(pdArray, parentCategory);
                    break;

                case PropertyDataCollection propCollection when parentName == "SystemProperties":
                    descriptors = ProcessWmiCollection<PropertyData>(
                        propCollection,
                        "System Properties",
                        (prop, _) => CreateSystemPropertyDescriptor(prop));
                    break;

                case PropertyDataCollection propCollection:
                    descriptors = ProcessWmiCollection<PropertyData>(
                        propCollection,
                        parentCategory,
                        CreatePropertyDataDescriptor);
                    break;

                case QualifierDataCollection qualifierCollection:
                    descriptors = ProcessWmiCollection<QualifierData>(
                        qualifierCollection,
                        "Qualifiers",
                        CreateQualifierDescriptor);
                    break;

                case QualifierData qualifierData when qualifierData.Value is Array qualifierArray:
                    descriptors = CreateArrayDescriptors(qualifierArray, parentCategory);
                    break;

                default:
                    descriptors = GetTypeProperties(value)
                        .Select(prop => new ReflectionPropertyDescriptor(prop, value, parentCategory));
                    break;
            }

            // Group by category (preserving order of first appearance), then sort within each group by DisplayName
            var grouped = descriptors
                .GroupBy(d => d.Category)
                .Select(g => new { Category = g.Key, Items = g.ToList() })
                .ToList();

            // Preserve the order of first appearance of categories
            foreach (var group in grouped)
            {
                foreach (var descriptor in group.Items.OrderBy(d => d.DisplayName))
                    yield return descriptor;
            }
        }

        /// <summary>
        /// Gets all property descriptors for the specified WMI object.
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetProperties(object? obj)
        {
            if (obj == null)
                yield break;

            _recursionDepth.Value = 0;

            IEnumerable<IPropertyDescriptor> descriptors = Enumerable.Empty<IPropertyDescriptor>();

            switch (obj)
            {
                case ManagementBaseObject mbo:
                    descriptors = GetManagementObjectProperties(mbo);
                    break;

                case PropertyDataCollection propertyCollection:
                    descriptors = ProcessWmiCollection<PropertyData>(
                        propertyCollection,
                        "Properties",
                        CreatePropertyDataDescriptor);
                    break;

                case QualifierDataCollection qualifierCollection:
                    descriptors = ProcessWmiCollection<QualifierData>(
                        qualifierCollection,
                        "Qualifiers",
                        CreateQualifierDescriptor);
                    break;

                default:
                    descriptors = GetTypeProperties(obj)
                        .Select(prop => new ReflectionPropertyDescriptor(prop, obj,
                            GetWmiCategoryForType(obj.GetType())));
                    break;
            }

            // Group by category (preserving order of first appearance), then sort within each group by DisplayName
            var grouped = descriptors
                .GroupBy(d => d.Category)
                .Select(g => new { Category = g.Key, Items = g.ToList() })
                .ToList();

            // Preserve the order of first appearance of categories
            foreach (var group in grouped)
            {
                foreach (var descriptor in group.Items.OrderBy(d => d.DisplayName))
                    yield return descriptor;
            }
        }        /// <summary>

                 /// Determines if the specified value represents a collection or complex object that can be expanded.
                 /// </summary>
        public bool IsExpandable(object? value, Type? valueType)
        {
            if (value == null)
                return false;

            Type type = value.GetType();

            // Check for specific WMI collection types by name
            string typeName = type.Name;
            if (typeName == "QualifierDataCollection" ||
                typeName == "PropertyDataCollection" ||
                (typeName.EndsWith("Collection") && typeName.Contains("Management")))
            {
                return true;
            }

            // More concise pattern matching for expandable types
            return value switch
            {
                PropertyData pd when pd.IsArray => true,
                PropertyDataCollection => true,
                QualifierDataCollection => true,
                ManagementBaseObject => true,
                ManagementPath => true,
                ManagementScope => true,
                ObjectGetOptions => true,
                ConnectionOptions => true,
                ICollection => true,  // Handle any ICollection implementation
                _ => false
            };
        }

        #region Helper classes

        /// <summary>
        /// Array property descriptor for WMI array values
        /// </summary>
        private class ArrayPropertyDescriptor : IPropertyDescriptor
        {
            private readonly Array _array;
            private readonly string _category;
            private readonly string _displayName;
            private readonly int _index;
            private readonly string _name;

            public ArrayPropertyDescriptor(Array array, int index, string name, string displayName, string category)
            {
                _array = array;
                _index = index;
                _name = name;
                _displayName = displayName;
                _category = category;
            }

            public string Category => _category;
            public string Description => $"Item at index {_index}";
            public string DisplayName => _displayName;
            public bool IsReadOnly => true;
            public string Name => _name;
            public Type? PropertyType => Value?.GetType() ?? _array.GetType().GetElementType();
            public object Source => _array;
            public object? Value => _array.GetValue(_index);

            public bool SetValue(object? value)
            {
                // WMI array values are typically not directly modifiable
                return false;
            }
        }

        /// <summary>
        /// Property descriptor for WMI qualifier data
        /// </summary>
        private class QualifierPropertyDescriptor : IPropertyDescriptor
        {
            private readonly string _category;
            private readonly QualifierData _qualifier;

            public QualifierPropertyDescriptor(QualifierData qualifier, string category)
            {
                _qualifier = qualifier;
                _category = category;
            }

            public string Category => _category;
            public string Description => $"WMI Qualifier: {_qualifier.Name}";
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

        /// <summary>
        /// Helper class for managing recursion depth with using statement
        /// </summary>
        private class RecursionScope : IDisposable
        {
            private readonly ThreadLocal<int> _depthCounter;

            public RecursionScope(ThreadLocal<int> depthCounter)
            {
                _depthCounter = depthCounter;
                _depthCounter.Value++;
                CurrentDepth = _depthCounter.Value;
            }

            public int CurrentDepth { get; }

            public void Dispose()
            {
                _depthCounter.Value--;
            }
        }

        #endregion Helper classes
    }
}