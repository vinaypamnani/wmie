using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Management;

namespace WmiExplorer.Common.Helpers
{
    public static class ValueMapHelper
    {
        private static readonly HashSet<string> ValueQualifiers = new(
            new[] { "values", "enumeration", "stringenumeration", "bitvalues", "bits" },
            StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ValueMapQualifiers = new(
            new[] { "valuemap", "bitmap" },
            StringComparer.OrdinalIgnoreCase);

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

        public static NameValueCollection? CreateNameValueCollection(object values, object? valueMap)
        {
            var result = new NameValueCollection();
            switch (values)
            {
                case string[] valueArray when valueMap is string[] mapArray && valueArray.Length == mapArray.Length:
                    for (int i = 0; i < valueArray.Length; i++)
                    {
                        result.Add(valueArray[i], mapArray[i]);
                    }
                    break;
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
            }
            return result.Count > 0 ? result : null;
        }
    }
} 