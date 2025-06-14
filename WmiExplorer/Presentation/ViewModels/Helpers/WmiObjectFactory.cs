using System.Management;

namespace WmiExplorer.Presentation.ViewModels.Helpers;

/// <summary>
/// Factory for creating and cleaning WMI objects for method parameter use.
/// Handles the complex logic of creating template objects and preparing them for WMI method calls.
/// </summary>
public static class WmiObjectFactory
{
    /// <summary>
    /// Cleans a parameter object for WMI method calls.
    /// Processes PropertyGrid values, converting "&lt;null&gt;" strings back to null and filtering meaningful values.
    /// </summary>
    /// <param name="source">The ManagementObject to clean</param>
    /// <returns>The same object with cleaned properties</returns>
    public static ManagementObject CleanParameterObject(ManagementObject source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        try
        {
            System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Cleaning parameter object");

            // Process properties: convert "<null>" back to null and identify meaningful values
            int meaningfulPropertyCount = 0;
            foreach (PropertyData prop in source.Properties)
            {
                if (prop.Name.StartsWith("__"))
                    continue;

                try
                {
                    // Check if this property has a meaningful value (this handles null conversion internally)
                    if (HasMeaningfulValue(prop))
                    {
                        System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Meaningful parameter property: {prop.Name} = {prop.Value}");
                        meaningfulPropertyCount++;
                    }
                }
                catch (Exception propEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Error processing property {prop.Name}: {propEx.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Parameter object has {meaningfulPropertyCount} meaningful properties");
            return source;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Error cleaning parameter object: {ex.Message}");
            return source; // Return as-is if cleaning fails
        }
    }

    /// <summary>
    /// Creates a template WMI object for method parameters.
    /// This avoids the issues with CreateInstance() for abstract or non-instantiable classes.
    /// </summary>
    /// <param name="className">The WMI class name to create a template for</param>
    /// <param name="scope">The management scope</param>
    /// <returns>A ManagementObject that can be used for parameter editing</returns>
    public static ManagementObject CreateTemplateObject(string className, ManagementScope scope)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Creating template object for {className}");

            var classPath = new ManagementPath($"{scope.Path.Path}:{className}");
            var managementClass = new ManagementClass(scope, classPath, null);

            // First try to create an actual instance (works for some classes)
            try
            {
                var instance = managementClass.CreateInstance();
                System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Created actual instance for {className}");
                return instance;
            }
            catch (Exception createEx)
            {
                System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] CreateInstance failed for {className}: {createEx.Message}");

                // Fallback: create a manual template object
                return CreateManualTemplateObject(className, managementClass);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Error creating template object for {className}: {ex.Message}");
            throw new InvalidOperationException($"Failed to create template object for class '{className}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a manual template object by copying properties from the management class.
    /// Used when CreateInstance() fails for abstract or non-instantiable classes.
    /// </summary>
    private static ManagementObject CreateManualTemplateObject(string className, ManagementClass managementClass)
    {
        var templateObject = new ManagementObject();

        foreach (PropertyData classProperty in managementClass.Properties)
        {
            try
            {
                // Skip system properties that might cause issues
                if (classProperty.Name.StartsWith("__"))
                    continue;

                // For method parameters, start with null values - only user-set properties will have values
                templateObject.Properties.Add(classProperty.Name, null, classProperty.Type);
            }
            catch (Exception propEx)
            {
                System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Error copying property '{classProperty.Name}': {propEx.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Created manual template object for {className} with {templateObject.Properties.Count} properties");
        return templateObject;
    }

    /// <summary>
    /// Determines if a property has a meaningful value for WMI method parameters.
    /// Also handles converting PropertyGrid's "<null>" representation back to actual null.
    /// </summary>
    private static bool HasMeaningfulValue(PropertyData prop)
    {
        if (prop.Value == null)
            return false;

        // For strings, handle PropertyGrid's "<null>" representation and check for meaningful content
        if (prop.Value is string strValue)
        {
            // Convert PropertyGrid's "<null>" representation back to actual null
            if (strValue.Trim() == "<null>" || strValue.Trim() == "null")
            {
                prop.Value = null;
                System.Diagnostics.Debug.WriteLine($"[WmiObjectFactory] Converted '<null>' back to null for property {prop.Name}");
                return false; // null values are not meaningful for parameters
            }

            return !string.IsNullOrWhiteSpace(strValue);
        }

        // For arrays, check if it has elements
        if (prop.IsArray && prop.Value is Array arrayValue)
        {
            return arrayValue.Length > 0;
        }

        // For other types, any non-null value is meaningful
        return true;
    }
}