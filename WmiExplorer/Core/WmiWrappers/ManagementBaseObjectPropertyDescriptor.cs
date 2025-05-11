using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.WmiWrappers
{
    /// <summary>
    /// Required for Implementation of ICustomTypeDescriptor
    /// Reference Links and Tutorials:
    /// http://www.codeproject.com/Articles/539202/CIMTool-for-Windows-Management-Instrumentation-Par
    /// http://www.codeproject.com/Articles/4448/Customized-display-of-collection-data-in-a-Propert
    /// http://msdn.microsoft.com/en-us/magazine/cc163816.aspx
    /// </summary>
    [TypeConverter(typeof(ManagementBaseObjectWConverter))]
    internal class ManagementBaseObjectPropertyDescriptor : PropertyDescriptor
    {
        private readonly string _category;
        private readonly PropertyData _property;
        private readonly ManagementBaseObjectW _wrapperObject;

        public ManagementBaseObjectPropertyDescriptor(ManagementBaseObjectW actualObject, PropertyData property)
            : base(property.Name, Array.Empty<Attribute>())
        {
            _wrapperObject = actualObject;
            _property = property;
            _category = _property.Origin == "___SYSTEM" ? "System Properties" : "Properties";
        }

        public override string Category => _category;

        public override Type ComponentType => _wrapperObject.GetType();

        public override string Description
        {
            get
            {
                // Embedded instance
                if (_property.Value is ManagementBaseObject || _property.Type == CimType.Object)
                {
                    string derivation;

                    try
                    {
                        var cimtype = _property.Qualifiers["CIMTYPE"];
                        derivation = cimtype != null && cimtype.Value != null
                            ? "Embedded " + cimtype.Value.ToString()
                            : "Embedded Object";
                    }
                    catch
                    {
                        derivation = "Embedded Object";
                    }

                    return derivation;
                }

                string retVal = "Type - " + _property.Type;

                if (_property.IsArray)
                    retVal += " []";

                if (_property.Type == CimType.DateTime && _property.Value != null && !_property.IsArray)
                    retVal = retVal + " - Normalized Value: " + GetNormalizedDate(_property);

                string pDesc = "";
                try
                {
                    ManagementClass mClass = new ManagementClass(_wrapperObject.ClassPath.Path);
                    mClass.Options.UseAmendedQualifiers = true;

                    if (mClass.Properties[_property.Name] != null)
                    {
                        foreach (QualifierData qd in mClass.Properties[_property.Name].Qualifiers)
                        {
                            if (qd.Name == "Description" && qd.Value != null)
                                pDesc = qd.Value.ToString() ?? "";

                            if (qd.Name == "lazy")
                                retVal += Environment.NewLine + "Lazy Property";

                            if (qd.Name == "enumeration" && qd.Value != null)
                                retVal += Environment.NewLine + "Enumeration: " + qd.Value;
                            // TODO: Find other qualifier containing enumerations (Values, ValueMap)
                        }
                    }
                }
                catch (Exception)
                {
                    pDesc = "";
                }

                if (!string.IsNullOrEmpty(pDesc))
                    retVal += Environment.NewLine + pDesc;

                return retVal;
            }
        }

        public override string DisplayName
        {
            get
            {
                string retVal = _property.Name;

                foreach (QualifierData q in _property.Qualifiers)
                {
                    if (q.Name == "key")
                        retVal = "*" + retVal;
                }

                return retVal;
            }
        }

        public override bool IsReadOnly => true;

        public override Type PropertyType => GetDotNetType();

        protected static object GetNormalizedDate(PropertyData propertyData)
        {
            if (propertyData.Type != CimType.DateTime || propertyData.Value == null)
                return propertyData.Value ?? string.Empty;

            //20080409032454.676631-420
            //   0  1  2  3   4  5   6  7   8  9  10 11   12 13 14  15 16 17 18 19 20
            // Y 2  0  0  8 M 0  4 D 0  9 H 0  3 M 2  4 S  5  4 .  m 6  7  6  6  3  1 U-420
            string dateString = propertyData.Value.ToString() ?? string.Empty;

            try
            {
                return new DateTime(
                    int.Parse(dateString.Substring(0, 4)),
                    int.Parse(dateString.Substring(4, 2)),
                    int.Parse(dateString.Substring(6, 2)),
                    int.Parse(dateString.Substring(8, 2)),
                    int.Parse(dateString.Substring(10, 2)),
                    int.Parse(dateString.Substring(12, 2)),
                    int.Parse(dateString.Substring(15, 6)) / 1000,
                    DateTimeKind.Local);
            }
            catch (ArgumentOutOfRangeException)
            {
                return dateString;
            }
        }

        public override bool CanResetValue(object? component)
        {
            return false;
        }

        public Type GetDotNetType()
        {
            if ((_property.Type == CimType.Object) && (_property.Value is ManagementBaseObject))
            {
                if (_property.Value is ManagementClass)
                {
                    return typeof(ManagementClass);
                }

                if (_property.Value is ManagementObject)
                {
                    return typeof(ManagementObject);
                }

                return typeof(ManagementBaseObject);
            }

            return ManagementBaseObjectW.GetTypeFor(_property.Type, _property.IsArray);
        }

        public override object? GetValue(object? component)
        {
            if (component is ManagementBaseObjectW managementComponent)
            {
                // To expand and display embedded instances, such as Props for SMS SCI classes.
                var val = managementComponent[_property.Name];

                if (val is ManagementBaseObject[] props)
                {
                    ManagementBaseObjectW[] propvalues = new ManagementBaseObjectW[props.Length];
                    for (int i = 0; i < props.Length; i++)
                    {
                        propvalues[i] = new ManagementBaseObjectW(props[i]);
                    }
                    return propvalues;
                }

                if (val is ManagementBaseObject props2)
                {
                    return new ManagementBaseObjectW(props2);
                }

                return val;
            }

            return null;
        }

        public override void ResetValue(object? component)
        {
            // No implementation needed as IsReadOnly is true
        }

        public override void SetValue(object? component, object? value)
        {
            if (component is ManagementBaseObjectW managementComponent && value != null)
            {
                managementComponent[_property.Name] = value;
            }
        }

        public override bool ShouldSerializeValue(object? component)
        {
            return true;
        }
    }
}