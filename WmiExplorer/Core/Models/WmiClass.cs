using System.Management;

namespace WmiExplorer.Core.Models
{
    public class WmiClass
    {
        // Keep the actual object for now since we're focused on refactoring WmiNamespace
        // In a future refactoring, this class could be updated as well
        private readonly ManagementBaseObject _actualObject;

        /// <summary>
        /// Constructor that takes required class information
        /// </summary>
        /// <param name="className">The class name</param>
        /// <param name="classPath">The namespace path containing this class</param>
        /// <param name="actualObject">The underlying WMI object</param>
        public WmiClass(string className, string classPath, ManagementBaseObject actualObject)
        {
            ClassName = className ?? throw new ArgumentNullException(nameof(className));
            ClassPath = classPath ?? throw new ArgumentNullException(nameof(classPath));
            _actualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
        }

        /// <summary>
        /// Returns the underlying WMI object
        /// </summary>
        public ManagementBaseObject ActualObject => _actualObject;

        /// <summary>
        /// Gets the class name
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// Gets the namespace path containing this class
        /// </summary>
        public string ClassPath { get; }

        /// <summary>
        /// Gets the class description from its qualifiers
        /// </summary>
        public string Description
        {
            get
            {
                try
                {
                    if (_actualObject?.Qualifiers == null)
                    {
                        return string.Empty;
                    }

                    // Look for Description qualifier and return its value
                    foreach (QualifierData qualifier in _actualObject.Qualifiers)
                    {
                        if (qualifier.Name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                        {
                            return _actualObject.GetQualifierValue("Description")?.ToString() ?? string.Empty;
                        }
                    }

                    return string.Empty;
                }
                catch (ManagementException ex)
                {
                    return ex.ErrorCode == ManagementStatus.NotFound
                        ? string.Empty
                        : "Error getting Class Description";
                }
            }
        }

        /// <summary>
        /// Returns the class's string representation
        /// </summary>
        public override string ToString() => ClassName;
    }
}