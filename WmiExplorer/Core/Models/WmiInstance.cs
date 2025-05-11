using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Model representing a WMI instance
    /// </summary>
    public class WmiInstance
    {
        /// <summary>
        /// Constructor that takes required instance information
        /// </summary>
        /// <param name="instanceName">The display name for this instance</param>
        /// <param name="actualObject">The underlying WMI object</param>
        public WmiInstance(string instanceName, ManagementObject actualObject)
        {
            InstanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
            ActualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
        }

        /// <summary>
        /// Gets the underlying WMI object
        /// </summary>
        public ManagementObject ActualObject { get; }

        /// <summary>
        /// Gets the display name of the instance
        /// </summary>
        public string InstanceName { get; }

        /// <summary>
        /// Returns the instance's string representation
        /// </summary>
        public override string ToString() => InstanceName;
    }
}