using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Thin wrapper for a WMI class ManagementObject
    /// </summary>
    public class WmiClass
    {
        public WmiClass(ManagementObject actualObject)
        {
            ActualObject = actualObject ?? throw new ArgumentNullException(nameof(actualObject));
        }

        public ManagementObject ActualObject { get; }

        // Optional: convenience property
        public string ClassName => ActualObject["__Class"]?.ToString() ?? string.Empty;

        public string ClassPath => ActualObject.Scope?.Path?.Path ?? string.Empty;

        public string Description => ActualObject.Qualifiers?["Description"]?.Value?.ToString() ?? string.Empty;
    }
}