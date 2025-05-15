using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Model representing a WMI instance
    /// </summary>
    public class WmiInstance
    {
        private readonly ManagementObject _actualObject;

        /// <summary>
        /// Constructor that takes required instance information
        /// </summary>
        /// <param name="actualObject">The underlying WMI object</param>
        public WmiInstance(ManagementObject actualObject)
        {
            _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
        }

        // Expose ManagementObject properties
        public ManagementPath Path => _actualObject.Path;
        public PropertyDataCollection Properties => _actualObject.Properties;
        public PropertyDataCollection SystemProperties => _actualObject.SystemProperties;
        public QualifierDataCollection Qualifiers => _actualObject.Qualifiers;
        public ManagementPath ClassPath => _actualObject.ClassPath;
        public string ClassName => _actualObject.ClassPath?.ClassName ?? string.Empty;
        public string ScopePath => _actualObject.Scope != null ? _actualObject.Scope.Path?.Path ?? string.Empty : string.Empty;
        public object this[string propertyName] => _actualObject[propertyName];

        /// <summary>
        /// Gets the display name of the instance
        /// </summary>
        public string InstanceName =>
            Path.RelativePath.ToString().Replace("\\\\", "\\") // TODO: Extract friendly name from known "Name" properties
            ?? string.Empty;

        /// <summary>
        /// Returns the instance's string representation
        /// </summary>
        public override string ToString() => InstanceName;
    }
}