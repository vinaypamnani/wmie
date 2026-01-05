using System.Collections;
using System.Management;
using System.Reflection;
using WmiExplorer.Models;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Providers;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyTypeProvider;

/// <summary>
/// Property provider that handles WMI-specific types like ManagementBaseObject and related WMI classes.
/// </summary>
public class WmiPropertyTypeProvider : IPropertyTypeProvider
{
    private readonly ISettingsService? _settingsService;

    public WmiPropertyTypeProvider(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
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
               objectType == typeof(WmiBaseObject) ||
               objectType == typeof(ManagementBaseObject) ||
               objectType == typeof(ManagementObject) ||
               objectType == typeof(ManagementBaseObject[]) ||
               objectType == typeof(ManagementObject[]) ||
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
            foreach (var desc in ProcessWmiCollection<PropertyData>(propertyCollection, string.Empty, (property, cat) => CreatePropertyDataDescriptor(property, string.Empty, value, true, propertyGridContext, false)))
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
            bool isTemplate = mbo is ManagementObject mo && IsTemplateObject(mo);
            foreach (var desc in ProcessWmiCollection<PropertyData>(mbo.Properties, string.Empty, (property, cat) => CreatePropertyDataDescriptor(property, string.Empty, mbo, false, propertyGridContext, isTemplate)))
                yield return desc;
            yield break;
        }
        // Special handling for array of embedded ManagementBaseObject
        if (value is ManagementBaseObject[] mboArray)
        {
            for (int i = 0; i < mboArray.Length; i++)
            {
                var embeddedMbo = mboArray[i];
                if (embeddedMbo != null)
                {
                    yield return new WmiEmbeddedObjectPropertyDescriptor($"[{i}]", embeddedMbo, value, parentCategory);
                }
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

        // Special handling for WmiBaseObject (preserve context)
        if (obj is WmiBaseObject wmiBaseObj)
        {
            var mbo = wmiBaseObj.ActualObject;
            foreach (PropertyData property in mbo.Properties)
            {
                yield return CreatePropertyDataDescriptor(property, "Output", wmiBaseObj, false, propertyGridContext, false);
            }
            foreach (PropertyData property in mbo.SystemProperties)
            {
                yield return CreatePropertyDataDescriptor(property, "System Properties", wmiBaseObj, false, propertyGridContext, false);
            }
            yield break;
        }

        // Special handling for ManagementObject - expose its properties directly (we used this for editing)
        if (obj is ManagementObject managementObject)
        {
            bool isTemplate = IsTemplateObject(managementObject);
            foreach (PropertyData property in managementObject.Properties)
            {
                yield return new WmiPropertyDescriptor(property, managementObject, "Properties", false, propertyGridContext, isTemplate, _settingsService); // forceEditable: true for template/parameter objects
            }
            yield break;
        }

        // Special handling for ManagementBaseObject (embedded objects in arrays)
        if (obj is ManagementBaseObject baseObject)
        {
            foreach (PropertyData property in baseObject.Properties)
            {
                yield return new WmiPropertyDescriptor(property, baseObject, "Properties", false, propertyGridContext, true, _settingsService);
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
    private IPropertyDescriptor CreatePropertyDataDescriptor(PropertyData property, string category, object? source, bool allowExpansion = false, IPropertyGridContext? propertyGridContext = null, bool forceEditable = false)
    {
        object? context = null;
        if (source is WmiBaseObject wmiBaseObj)
        {
            context = wmiBaseObj.Context;
            source = wmiBaseObj.ActualObject;
        }
        else if (source is WmiInstance wmiInstance)
        {
            source = wmiInstance.ActualObject ?? new ManagementClass();
        }
        else if (source is not ManagementBaseObject)
        {
            source = new ManagementClass();
        }
        return new WmiPropertyDescriptor(property, (ManagementBaseObject)source, category, context, allowExpansion, propertyGridContext, forceEditable, _settingsService);
    }

    /// <summary>
    /// Creates a property descriptor for QualifierData
    /// </summary>
    private IPropertyDescriptor CreateQualifierDescriptor(QualifierData qualifier, string category, object? source = null)
    {
        return new WmiQualifierPropertyDescriptor(qualifier, category, source);
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
    /// Gets properties using reflection for regular .NET objects
    /// </summary>
    private static IEnumerable<PropertyInfo> GetTypeProperties(object obj)
    {
        return obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
    }

    private static bool IsTemplateObject(ManagementObject obj)
    {
        foreach (PropertyData prop in obj.Properties)
        {
            if (prop.Name.StartsWith("__"))
                continue;
            if (prop.Value == null)
                continue;
            // Treat 0 as unset for numeric types
            var type = prop.Value.GetType();
            if (type == typeof(int) && (int)prop.Value == 0)
                continue;
            if (type == typeof(uint) && (uint)prop.Value == 0)
                continue;
            if (type == typeof(long) && (long)prop.Value == 0)
                continue;
            if (type == typeof(ulong) && (ulong)prop.Value == 0)
                continue;
            if (type == typeof(short) && (short)prop.Value == 0)
                continue;
            if (type == typeof(ushort) && (ushort)prop.Value == 0)
                continue;
            if (type == typeof(byte) && (byte)prop.Value == 0)
                continue;
            if (type == typeof(sbyte) && (sbyte)prop.Value == 0)
                continue;
            if (type == typeof(float) && (float)prop.Value == 0f)
                continue;
            if (type == typeof(double) && (double)prop.Value == 0.0)
                continue;
            if (type == typeof(decimal) && (decimal)prop.Value == 0m)
                continue;
            // If we get here, the property is set to a non-null, non-zero value
            return false;
        }
        return true;
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