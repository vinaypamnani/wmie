using System;
using System.Collections.Generic;
using System.Linq;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;
using WmiExplorer.Presentation.Controls.PropertyGrid.Providers;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Presentation.PropertyTypeProvider
{
    /// <summary>
    /// Property type provider for WmiInstance, delegating to the underlying PropertyDataCollection.
    /// </summary>
    public class WmiInstancePropertyTypeProvider : IPropertyTypeProvider
    {
        public bool CanHandle(Type? objectType)
        {
            return objectType != null && typeof(WmiInstance).IsAssignableFrom(objectType);
        }

        public IEnumerable<IPropertyDescriptor> GetProperties(object? obj)
        {
            if (obj is not WmiInstance wmiInstance || wmiInstance.Properties == null)
                yield break;

            // Use the existing WmiPropertyTypeProvider to extract descriptors
            var wmiProvider = new BaseWmiPropertyTypeProvider();
            foreach (var descriptor in wmiProvider.GetProperties(wmiInstance.Properties))
                yield return descriptor;

            foreach (var descriptor in wmiProvider.GetProperties(wmiInstance.SystemProperties))
                yield return descriptor;

            foreach (var descriptor in wmiProvider.GetProperties(wmiInstance))
                yield return descriptor;
        }

        public IEnumerable<IPropertyDescriptor> GetChildItems(object? value, string parentName, string parentCategory)
        {
            if (value is not WmiInstance wmiInstance || wmiInstance.Properties == null)
                yield break;

            var wmiProvider = new BaseWmiPropertyTypeProvider();

            foreach (var descriptor in wmiProvider.GetChildItems(wmiInstance.Properties, parentName, parentCategory))
                yield return descriptor;            
        }

        public bool IsExpandable(object? value, Type? valueType)
        {
            if (value is WmiInstance)
                return true;

            return false;
        }
    }
}
