using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Minimal container for a WMI namespace object
    /// </summary>
    public class WmiNamespace
    {
        /// <summary>
        /// Constructor that takes the WMI object
        /// </summary>
        public WmiNamespace(ManagementBaseObject? actualObject, string fullPath)
        {
            ActualObject = actualObject;
            FullPath = fullPath ?? throw new ArgumentNullException(nameof(fullPath));
            IsRoot = false; // Default to false
        }

        /// <summary>
        /// Constructor that takes the WMI object and specifies if it's a root namespace
        /// </summary>
        public WmiNamespace(ManagementBaseObject? actualObject, string fullPath, bool isRoot)
        {
            ActualObject = actualObject;
            FullPath = fullPath ?? throw new ArgumentNullException(nameof(fullPath));
            IsRoot = isRoot;
        }

        /// <summary>
        /// The underlying WMI object, if available
        /// </summary>
        public ManagementBaseObject? ActualObject { get; }
        
        /// <summary>
        /// The full path of the namespace (needed for root namespace which has no ActualObject)
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Indicates whether this namespace is the root namespace
        /// </summary>
        public bool IsRoot { get; set; }

        /// <summary>
        /// Returns the string representation
        /// </summary>
        public override string ToString() => FullPath;
    }
}