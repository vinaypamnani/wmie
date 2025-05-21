using System;
using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Represents a WMI event received from a watcher.
    /// </summary>
    public class WmiEvent
    {
        public string WatcherName { get; }

        [Category("Event Information")]
        public DateTime EventTimestamp { get; }

        public string EventClassName { get; }

        [Category("Event Information")]
        public string EventRelativePath { get; }

        [Category("Event")]
        [ExpandProperty]
        public ManagementBaseObject EventData { get; }

        public WmiEvent(string watcherName, ManagementBaseObject eventData)
        {
            WatcherName = watcherName ?? throw new ArgumentNullException(nameof(watcherName));
            EventData = eventData as ManagementBaseObject ?? throw new ArgumentNullException(nameof(eventData));
            EventTimestamp = DateTime.Now;
            var targetInstance = eventData["TargetInstance"] as ManagementBaseObject;
            EventRelativePath = targetInstance?["__RELPATH"]?.ToString() ?? "Unknown";
            EventClassName = eventData.ClassPath?.ClassName ?? "Unknown";
        }
    }
}