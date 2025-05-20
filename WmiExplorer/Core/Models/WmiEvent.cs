using System;
using System.Management;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Represents a WMI event received from a watcher.
    /// </summary>
    public class WmiEvent
    {
        public string WatcherName { get; }
        public DateTime Timestamp { get; }
        public string ClassPath { get; }
        public ManagementBaseObject EventData { get; }

        public WmiEvent(string watcherName, ManagementBaseObject eventData)
        {
            WatcherName = watcherName ?? throw new ArgumentNullException(nameof(watcherName));
            EventData = eventData ?? throw new ArgumentNullException(nameof(eventData));
            Timestamp = DateTime.Now;
            ClassPath = eventData.ClassPath?.ClassName ?? "Unknown";
        }
    }
} 