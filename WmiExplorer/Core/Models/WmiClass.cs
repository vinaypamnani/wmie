using System.Management;
using System.ComponentModel;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Thin wrapper for a WMI class ManagementClass
    /// </summary>
    public class WmiClass
    {
        private ManagementClass _actualClass;

        public WmiClass(ManagementBaseObject actualClass)
        {            
            
            _actualClass = (ManagementClass)actualClass ?? throw new ArgumentNullException(nameof(actualClass));            

            // Initialize the Methods collection and populate it with WmiMethod objects, encapsulating the MethodData objects so retrieval is fast.
            // This is done in the constructor to avoid having to do it in the property getter, which would be slow.
            Methods = new List<WmiMethod>();
            foreach (MethodData method in _actualClass.Methods)
            {
                Methods.Add(new WmiMethod(method));
            }            
        }
        
        [Category("Class")]
        public string ClassName => _actualClass["__Class"]?.ToString() ?? string.Empty;

        [Category("Metadata")]
        public string[] Derivation => _actualClass.Derivation?.Cast<string>().ToArray() ?? new string[0];

        [Category("Metadata")]
        public string SuperClass => (_actualClass.Derivation != null && _actualClass.Derivation.Count > 0 && _actualClass.Derivation[0] != null) ? _actualClass.Derivation[0]! : string.Empty;

        [Category("Metadata")]
        public ManagementPath ClassPath => _actualClass.ClassPath;

        [Browsable(false)]
        public string Description
        {
            get
            {
                try
                {
                    if (_actualClass.Qualifiers != null && _actualClass.Qualifiers.Cast<QualifierData>().Any(q => q.Name == "Description"))
                        return _actualClass.Qualifiers["Description"]?.Value?.ToString() ?? string.Empty;
                }
                catch
                {
                    // Optionally log the error
                }
                return string.Empty;
            }
        }

        [Category("Methods")]
        public List<WmiMethod> Methods { get; }

        [Category("Qualifiers")]
        public QualifierDataCollection Qualifiers => _actualClass.Qualifiers;

        [Category("Metadata")]
        public PropertyDataCollection Properties => _actualClass.Properties;

        [Category("Metadata")]
        public PropertyDataCollection SystemProperties => _actualClass.SystemProperties;

        [Category("Metadata")]
        public ManagementPath Path => _actualClass.Path;

        [Category("Metadata")]
        public ManagementScope Scope => _actualClass.Scope;        
    }
}