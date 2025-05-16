using System.ComponentModel;
using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Model representing a WMI instance
    /// </summary>
    public class WmiInstance
    {
        private readonly ManagementObject _actualObject;

        [Browsable(false)]
        public ManagementObject ActualObject => _actualObject; // needed for PropertyTypeProvider to expose property description

        /// <summary>
        /// Constructor that takes required instance information
        /// </summary>
        /// <param name="actualObject">The underlying WMI object</param>
        public WmiInstance(ManagementObject actualObject)
        {
            _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));            
        }

        // Expose ManagementObject properties        

        [Category("Properties")]
        public PropertyDataCollection Properties => _actualObject.Properties;

        [Category("System Properties")]
        public PropertyDataCollection SystemProperties => _actualObject.SystemProperties;

        [Category("Qualifiers")]
        public QualifierDataCollection Qualifiers => _actualObject.Qualifiers;

        public ManagementPath Path => _actualObject.Path;

        public ManagementPath ClassPath => _actualObject.ClassPath;

        public ObjectGetOptions Options => _actualObject.Options;

        public ManagementScope Scope => _actualObject.Scope;

        public object this[string propertyName] => _actualObject[propertyName];

        /// <summary>
        /// Gets the display name of the instance
        /// </summary>
        [Browsable(false)]
        public string InstanceName =>
            Path.RelativePath.ToString().Replace("\\\\", "\\") // TODO: Extract friendly name from known "Name" properties
            ?? string.Empty;

        /// <summary>
        /// Returns the instance's string representation
        /// </summary>
        public override string ToString() => $"Instance: {InstanceName}";
    }
}