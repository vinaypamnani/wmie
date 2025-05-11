using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Minimal container for a WMI namespace object
    /// </summary>
    public class WmiNamespace
    {
        /// <summary>
        /// Constructor for a WMI namespace, optionally with ConnectionOptions (root if specified)
        /// </summary>
        public WmiNamespace(ManagementObject? actualObject, string namespacePath, ConnectionOptions? connectionOptions = null)
        {
            ActualObject = actualObject;
            NamespacePath = namespacePath ?? throw new ArgumentNullException(nameof(namespacePath));
            ConnectionOptions = connectionOptions;
            IsRoot = true;
        }

        /// <summary>
        /// Constructor for a child WMI namespace, propagating ConnectionOptions from the parent
        /// </summary>
        public WmiNamespace(ManagementObject? actualObject, string namespacePath, WmiNamespace parent)
        {
            ActualObject = actualObject;
            NamespacePath = namespacePath ?? throw new ArgumentNullException(nameof(namespacePath));
            ConnectionOptions = parent?.ConnectionOptions;
            IsRoot = false;
        }

        /// <summary>
        /// The underlying WMI object, if available
        /// </summary>
        public ManagementObject? ActualObject { get; }

        /// <summary>
        /// The path of the namespace (e.g., "root\\cimv2")
        /// </summary>
        public string NamespacePath { get; }

        /// <summary>
        /// The ConnectionOptions used for this namespace (can be null)
        /// </summary>
        public ConnectionOptions? ConnectionOptions { get; }

        /// <summary>
        /// Indicates whether this namespace is the root namespace (if ConnectionOptions is specified)
        /// </summary>
        public bool IsRoot { get; }

        /// <summary>
        /// Returns the string representation
        /// </summary>
        public override string ToString() => NamespacePath;
    }
}