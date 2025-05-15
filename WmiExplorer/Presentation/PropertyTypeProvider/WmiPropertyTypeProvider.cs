using System.Collections;
using System.Management;
using System.Reflection;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;
using WmiExplorer.Presentation.Controls.PropertyGrid.Providers;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    /// <summary>
    /// Property provider that handles WMI-specific types like ManagementBaseObject and related WMI classes.
    /// </summary>
    public class WmiPropertyTypeProvider : IPropertyTypeProvider
    {
        /// <summary>
        /// Gets properties using reflection for regular .NET objects
        /// </summary>
        private static IEnumerable<PropertyInfo> GetTypeProperties(object obj)
        {
            return obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        }

        /// <summary>
        /// Helper to get category from property attribute
        /// </summary>
        private static string GetCategoryOrDefault(PropertyInfo prop)
        {
            var categoryAttr = prop.GetCustomAttribute<System.ComponentModel.CategoryAttribute>();
            return categoryAttr?.Category ?? "Misc"; // Use "Misc" as the default if no attribute
        }

        /// <summary>
        /// Creates a property descriptor for PropertyData
        /// </summary>
        private IPropertyDescriptor CreatePropertyDataDescriptor(PropertyData property, string category, object? source)
        {
            // Use the ActualObject property of WmiInstance if available, otherwise fallback to dummy
            var wmiSource = (source is WmiInstance wmiInstance)
                ? (wmiInstance.ActualObject ?? new ManagementClass())
                : (source as ManagementBaseObject ?? new ManagementClass());
            return new WmiPropertyDescriptor(property, wmiSource);
        }

        /// <summary>
        /// Creates a property descriptor for QualifierData
        /// </summary>
        private IPropertyDescriptor CreateQualifierDescriptor(QualifierData qualifier, string category)
        {
            return new WmiQualifierPropertyDescriptor(qualifier, category);
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
            catch
            {
                yield break;
            }

            // Yield all the items from our safely extracted list
            if (items != null)
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
            // Handle WmiInstance by type, and WMI types
            return objectType == typeof(WmiClass) ||
                   objectType == typeof(WmiInstance) ||
                   objectType == typeof(ManagementBaseObject) ||
                   objectType == typeof(PropertyDataCollection) ||
                   objectType == typeof(QualifierDataCollection);
        }

        /// <summary>
        /// Gets all property descriptors for the specified WMI object.
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetProperties(object? obj)
        {
            return GetProperties(obj, null);
        }

        public IEnumerable<IPropertyDescriptor> GetProperties(object? obj, object? source = null)
        {
            if (obj == null)
                yield break;

            var type = obj.GetType();
            bool yieldedSpecial = false;
            foreach (var prop in GetTypeProperties(obj))
            {
                var category = GetCategoryOrDefault(prop);
                // Special handling for PropertyDataCollection: only for WmiInstance
                if (obj is WmiInstance && typeof(PropertyDataCollection).IsAssignableFrom(prop.PropertyType))
                {
                    var propertyDataCollection = prop.GetValue(obj) as PropertyDataCollection;
                    if (propertyDataCollection != null)
                    {
                        foreach (var desc in ProcessWmiCollection<PropertyData>(propertyDataCollection, category, (property, cat) => CreatePropertyDataDescriptor(property, cat, obj)))
                            yield return desc;
                        yieldedSpecial = true;
                        continue;
                    }
                }
                // Special handling for QualifierDataCollection: for WmiInstance and WmiClass
                if ((obj is WmiInstance || obj is WmiClass) && typeof(QualifierDataCollection).IsAssignableFrom(prop.PropertyType))
                {
                    var qualifierDataCollection = prop.GetValue(obj) as QualifierDataCollection;
                    if (qualifierDataCollection != null)
                    {
                        foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierDataCollection, category, (qualifier, cat) => CreateQualifierDescriptor(qualifier, cat)))
                            yield return desc;
                        yieldedSpecial = true;
                        continue;
                    }
                }
                yield return new DefaultPropertyDescriptor(prop, obj, category);
            }
            
            // For standalone PropertyDataCollection or QualifierDataCollection, use type name as category
            // We will never assign these directly to PropertyGrid but keeping it just in case.
            if (!yieldedSpecial && obj is PropertyDataCollection propertyCollection)
            {
                var category = propertyCollection.GetType().Name;
                foreach (var desc in ProcessWmiCollection<PropertyData>(propertyCollection, category, (property, cat) => CreatePropertyDataDescriptor(property, cat, source)))
                    yield return desc;
            }
            if (!yieldedSpecial && obj is QualifierDataCollection qualifierCollection)
            {
                var category = qualifierCollection.GetType().Name;
                foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierCollection, category, (qualifier, cat) => CreateQualifierDescriptor(qualifier, cat)))
                    yield return desc;
            }
        }

        /// <summary>
        /// Gets child items for an expandable property value (collection or complex object).
        /// </summary>
        public IEnumerable<IPropertyDescriptor> GetChildItems(object? value, string parentName, string parentCategory)
        {
            if (value == null)
                yield break;

            // Special handling for PropertyDataCollection and QualifierDataCollection
            if (value is PropertyDataCollection propertyCollection)
            {
                var category = propertyCollection.GetType().Name;
                foreach (var desc in ProcessWmiCollection<PropertyData>(propertyCollection, category, (property, cat) => CreatePropertyDataDescriptor(property, cat, value)))
                    yield return desc;
                yield break;
            }

            if (value is QualifierDataCollection qualifierCollection)
            {
                var category = qualifierCollection.GetType().Name;
                foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierCollection, category, (qualifier, cat) => CreateQualifierDescriptor(qualifier, cat)))
                    yield return desc;
                yield break;
            }

            foreach (var prop in GetTypeProperties(value))
                yield return new DefaultPropertyDescriptor(prop, value, GetCategoryOrDefault(prop));
        }

        /// <summary>
        /// Determines if the specified value represents a collection or complex object that can be expanded.
        /// </summary>
        public bool IsExpandable(object? value, Type? valueType)
        {
            if (value == null)
                return false;

            // More concise pattern matching for expandable types
            return value switch
            {
                PropertyData pd when pd.IsArray => true,
                PropertyDataCollection => true,
                QualifierDataCollection => true,
                ManagementBaseObject => true,
                ICollection => true,  // Handle any ICollection implementation
                _ => false
            };
        }
    }
}