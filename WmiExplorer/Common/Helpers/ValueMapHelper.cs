using System.Collections.Specialized;
using System.Management;

namespace WmiExplorer.Common.Helpers;

public static class ValueMapHelper
{
    private static readonly HashSet<string> ValueMapQualifiers = new(
        new[] { "valuemap", "bitmap" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValueQualifiers = new(
        new[] { "values", "enumeration", "stringenumeration", "bitvalues", "bits" },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a NameValueCollection from the value map (codes) and values (display strings).
    /// valueMap: code array (keys), values: display string array (values)
    /// </summary>
    public static NameValueCollection? CreateNameValueCollection(object? valueMap, object? values)
    {
        var result = new NameValueCollection();
        if (valueMap == null && values == null)
            return null;
        switch (valueMap)
        {
            case string[] mapArray when values is string[] valueArray && valueArray.Length == mapArray.Length:
                for (int i = 0; i < mapArray.Length; i++)
                {
                    result.Add(mapArray[i], valueArray[i]); // key = valueMap, value = display string
                }
                break;
            case string[] mapArray:
                for (int i = 0; i < mapArray.Length; i++)
                {
                    result.Add(mapArray[i], mapArray[i]);
                }
                break;
            case int[] intArray:
                for (int i = 0; i < intArray.Length; i++)
                {
                    var value = intArray[i].ToString();
                    result.Add(value, value);
                }
                break;
            case string str when !string.IsNullOrEmpty(str):
                var splitValues = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var splitValue in splitValues)
                {
                    var value = splitValue.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value, value);
                    }
                }
                break;
            case null:
                // If valueMap is null, try to use values as the source
                switch (values)
                {
                    case string[] valueArray:
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            result.Add(valueArray[i], valueArray[i]);
                        }
                        break;
                    case int[] intArray:
                        for (int i = 0; i < intArray.Length; i++)
                        {
                            var value = intArray[i].ToString();
                            result.Add(value, value);
                        }
                        break;
                    case string str2 when !string.IsNullOrEmpty(str2):
                        var splitValues2 = str2.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var splitValue in splitValues2)
                        {
                            var value = splitValue.Trim();
                            if (!string.IsNullOrEmpty(value))
                            {
                                result.Add(value, value);
                            }
                        }
                        break;
                }
                break;
        }
        return result.Count > 0 ? result : null;
    }

    public static void GetPossibleValuesAndMap(QualifierDataCollection? qualifiers, out object? values, out object? valueMap)
    {
        values = null;
        valueMap = null;
        if (qualifiers == null)
            return;
        foreach (var name in ValueQualifiers)
        {
            values = GetQualifierValue(qualifiers, name);
            if (values != null)
                break;
        }
        foreach (var name in ValueMapQualifiers)
        {
            valueMap = GetQualifierValue(qualifiers, name);
            if (valueMap != null)
                break;
        }
    }

    public static object? GetQualifierValue(QualifierDataCollection? qualifiers, string qualifierName)
    {
        if (qualifiers == null)
            return null;
        foreach (QualifierData qualifier in qualifiers)
        {
            if (qualifier.Name.Equals(qualifierName, StringComparison.OrdinalIgnoreCase))
            {
                return qualifier.Value;
            }
        }
        return null;
    }
}