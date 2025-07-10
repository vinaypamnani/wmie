using System.Collections;
using System.Management;
using System.Reflection;
using WmiExplorer.Core.Models;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Providers;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property provider that handles WMI-specific types like ManagementBaseObject and related WMI classes.
/// </summary>
public class WmiPropertyTypeProvider : IPropertyTypeProvider
{
    private readonly IWmiService? _wmiService;

    public WmiPropertyTypeProvider(IWmiService? wmiService = null)
    {
        _wmiService = wmiService;
    }

    /// <summary>
    /// Determines if this provider can handle the specified object type.
    /// </summary>
    public bool CanHandle(Type? objectType)
    {
        if (objectType == null)
            return false;
        // Handle WmiInstance by type, and WMI types
        return objectType == typeof(WmiNamespace) ||
               objectType == typeof(WmiClass) ||
               objectType == typeof(WmiInstance) ||
               objectType == typeof(ManagementBaseObject) ||
               objectType == typeof(ManagementObject) ||
               objectType == typeof(PropertyDataCollection) ||
               objectType == typeof(QualifierDataCollection) == true;
    }

    /// <summary>
    /// Gets child items for an expandable property value (collection or complex object).
    /// </summary>
    public IEnumerable<IPropertyDescriptor> GetChildItems(object? value, string parentName, string parentCategory, IPropertyGridContext? propertyGridContext = null)
    {
        if (value == null)
            yield break;

        // Special handling for PropertyDataCollection and QualifierDataCollection
        if (value is PropertyDataCollection propertyCollection)
        {
            foreach (var desc in ProcessWmiCollection<PropertyData>(propertyCollection, string.Empty, (property, cat) => CreatePropertyDataDescriptor(property, string.Empty, value, true, propertyGridContext)))
                yield return desc;
            yield break;
        }

        if (value is QualifierDataCollection qualifierCollection)
        {
            foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierCollection, string.Empty, (qualifier, cat) => CreateQualifierDescriptor(qualifier, string.Empty, value)))
                yield return desc;
            yield break;
        }

        // Special handling for embedded ManagementBaseObject
        if (value is ManagementBaseObject mbo)
        {
            foreach (var desc in ProcessWmiCollection<PropertyData>(mbo.Properties, string.Empty, (property, cat) => CreatePropertyDataDescriptor(property, string.Empty, mbo, false, propertyGridContext)))
                yield return desc;
            yield break;
        }
        // Special handling for array of embedded ManagementBaseObject
        if (value is ManagementBaseObject[] mboArray)
        {
            foreach (var embeddedMbo in mboArray)
            {
                foreach (var desc in ProcessWmiCollection<PropertyData>(embeddedMbo.Properties, string.Empty, (property, cat) => CreatePropertyDataDescriptor(property, string.Empty, embeddedMbo, false, propertyGridContext)))
                    yield return desc;
            }
            yield break;
        }

        foreach (var prop in GetTypeProperties(value))
            yield return new DefaultPropertyDescriptor(prop, value, GetCategoryOrDefault(prop));
    }

    /// <summary>
    /// Gets all property descriptors for the specified WMI object.
    /// </summary>
    public IEnumerable<IPropertyDescriptor> GetProperties(object? obj, IPropertyGridContext? propertyGridContext = null)
    {
        if (obj == null)
            yield break;

        // Special handling for ManagementBaseObject - expose its properties directly (we used this for editing)
        if (obj is ManagementObject managementObject)
        {
            foreach (PropertyData property in managementObject.Properties)
            {
                yield return new WmiPropertyDescriptor(property, managementObject, "Properties", false, propertyGridContext);
            }
            yield break;
        }

        var type = obj.GetType();
        bool yieldedSpecial = false;
        foreach (var prop in GetTypeProperties(obj))
        {
            var category = GetCategoryOrDefault(prop);

            // Special handling for PropertyDataCollection: we want Properties to be exposed on top-level only for WmiInstance
            if (obj is WmiInstance && typeof(PropertyDataCollection).IsAssignableFrom(prop.PropertyType))
            {
                var propertyDataCollection = prop.GetValue(obj) as PropertyDataCollection;
                if (propertyDataCollection != null)
                {
                    foreach (var desc in ProcessWmiCollection<PropertyData>(propertyDataCollection, category, (property, cat) => CreatePropertyDataDescriptor(property, cat, obj, false, propertyGridContext)))
                        yield return desc;
                    yieldedSpecial = true;
                    continue;
                }
            }

            // Special handling for QualifierDataCollection: we want Qualifiers to be exposed on top-level for WmiInstance and WmiClass
            if ((obj is WmiInstance || obj is WmiClass || obj is WmiNamespace) && typeof(QualifierDataCollection).IsAssignableFrom(prop.PropertyType))
            {
                var qualifierDataCollection = prop.GetValue(obj) as QualifierDataCollection;
                if (qualifierDataCollection != null)
                {
                    foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierDataCollection, category, (qualifier, cat) => CreateQualifierDescriptor(qualifier, cat, obj)))
                        yield return desc;
                    yieldedSpecial = true;
                    continue;
                }
            }

            // Special handling for Methods: expose as top-level for WmiClass
            if (obj is WmiClass && typeof(List<WmiMethod>).IsAssignableFrom(prop.PropertyType))
            {
                var methods = prop.GetValue(obj) as List<WmiMethod>;
                if (methods != null)
                {
                    foreach (var method in methods)
                    {
                        yield return new WmiMethodPropertyDescriptor(method, category);
                    }
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
            foreach (var desc in ProcessWmiCollection<PropertyData>(propertyCollection, category, (property, cat) => CreatePropertyDataDescriptor(property, cat, propertyGridContext)))
                yield return desc;
        }
        if (!yieldedSpecial && obj is QualifierDataCollection qualifierCollection)
        {
            var category = qualifierCollection.GetType().Name;
            foreach (var desc in ProcessWmiCollection<QualifierData>(qualifierCollection, category, (qualifier, cat) => CreateQualifierDescriptor(qualifier, cat, propertyGridContext)))
                yield return desc;
        }
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

    /// <summary>
    /// Creates a property descriptor for PropertyData
    /// </summary>
    private IPropertyDescriptor CreatePropertyDataDescriptor(PropertyData property, string category, object? source, bool allowExpansion = false, IPropertyGridContext? propertyGridContext = null)
    {
        // Use the ActualObject property of WmiInstance if available, otherwise fallback to dummy
        var wmiSource = (source is WmiInstance wmiInstance)
            ? (wmiInstance.ActualObject ?? new ManagementClass())
            : (source as ManagementBaseObject ?? new ManagementClass());
        return new WmiPropertyDescriptor(property, wmiSource, category, allowExpansion, propertyGridContext);
    }

    /// <summary>
    /// Creates a property descriptor for QualifierData
    /// </summary>
    private IPropertyDescriptor CreateQualifierDescriptor(QualifierData qualifier, string category, object? source = null)
    {
        string? providerClsid = null;
        if (_wmiService != null && string.Equals(qualifier.Name, "provider", StringComparison.OrdinalIgnoreCase) && qualifier.Value is string providerName)
        {
            var scope = GetManagementScopeFromSource(source);
            if (scope != null)
                providerClsid = _wmiService.GetProviderClsid(scope, providerName);
        }
        return new WmiQualifierPropertyDescriptor(qualifier, category, providerClsid);
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
    /// Helper to extract ConnectionOptions from various WMI-related source objects
    /// </summary>
    private static ConnectionOptions? GetConnectionOptionsFromSource(object? source)
    {
        if (source is ManagementObject mo && mo.Scope != null)
            return mo.Scope.Options;
        if (source is ManagementClass mc && mc.Scope != null)
            return mc.Scope.Options;
        if (source is WmiClass wmiClass && wmiClass.Scope != null)
            return wmiClass.Scope.Options;
        if (source is WmiInstance wmiInstance && wmiInstance.Scope != null)
            return wmiInstance.Scope.Options;
        if (source is WmiNamespace wmiNamespace && wmiNamespace.ConnectionOptions != null)
            return wmiNamespace.ConnectionOptions;
        return null;
    }

    /// <summary>
    /// Helper to extract ManagementScope from various WMI-related source objects
    /// </summary>
    private static ManagementScope? GetManagementScopeFromSource(object? source)
    {
        if (source is ManagementObject mo && mo.Scope != null)
            return mo.Scope;
        if (source is ManagementClass mc && mc.Scope != null)
            return mc.Scope;
        if (source is WmiClass wmiClass && wmiClass.Scope != null)
            return wmiClass.Scope;
        if (source is WmiInstance wmiInstance && wmiInstance.Scope != null)
            return wmiInstance.Scope;
        return null;
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
}