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

        public string EventClassName { get; }

        [Category("Event")]
        public DateTime EventTimestamp { get; }

        [Category("Event")]
        public string EventRelativePath { get; }

        [Category("Event")]
        [ShowChildrenAsParent]
        public ManagementBaseObject EventData { get; }

        [Category("Event")]
        [ShowChildrenAsParent]
        public PropertyChangeTracker? EventChanges { get; }

        public WmiEvent(string watcherName, ManagementBaseObject eventData)
        {
            WatcherName = watcherName ?? throw new ArgumentNullException(nameof(watcherName));
            EventData = eventData as ManagementBaseObject ?? throw new ArgumentNullException(nameof(eventData));
            EventTimestamp = DateTime.Now;

            // Generic handling: find any property with name starting with "Target" or "Previous"
            ManagementBaseObject? targetObject = null;
            ManagementBaseObject? previousObject = null;
            try
            {
                foreach (PropertyData prop in eventData.Properties)
                {
                    if (prop.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase) && prop.Value is ManagementBaseObject mboTarget)
                    {
                        targetObject ??= mboTarget;
                    }
                    else if (prop.Name.StartsWith("Previous", StringComparison.OrdinalIgnoreCase) && prop.Value is ManagementBaseObject mboPrev)
                    {
                        previousObject ??= mboPrev;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Embedded instance access error: {ex.Message}");
            }

            EventRelativePath = targetObject?["__RELPATH"]?.ToString() ?? "<Unknown>";
            EventClassName = eventData.ClassPath?.ClassName ?? "<Unknown>";

            if (targetObject != null && previousObject != null)
            {
                EventChanges = GetPropertyDifferences(previousObject, targetObject);
            }
        }

        /// <summary>
        /// Utility to diff two ManagementBaseObject instances.
        /// </summary>
        public static PropertyChangeTracker GetPropertyDifferences(ManagementBaseObject? previous, ManagementBaseObject? current)
        {
            var diff = new PropertyChangeTracker();
            if (previous == null || current == null)
                return diff;

            foreach (PropertyData property in current.Properties)
            {
                var propName = property.Name;
                var newValue = property.Value;
                var oldValue = previous.Properties[propName]?.Value;
                if (!Equals(newValue, oldValue))
                {
                    diff.ChangedProperties.Add(new PropertyChangeTracker.DiffEntry
                    {
                        PropertyName = propName,
                        OldValue = oldValue,
                        NewValue = newValue
                    });
                }
            }
            return diff;
        }
    }

    /// <summary>
    /// Represents the difference between two WMI instances.
    /// </summary>
    public class PropertyChangeTracker
    {
        public class DiffEntry
        {
            public string PropertyName { get; set; } = string.Empty;
            public object? OldValue { get; set; }
            public object? NewValue { get; set; }

            public override string ToString()
            {
                return $"{PropertyName}: {OldValue} -> {NewValue}";
            }
        }

        [ExpandByDefault]
        public List<DiffEntry> ChangedProperties { get; } = new List<DiffEntry>();

        public override string ToString()
        {
            return "Changed Properties: " + ChangedProperties.Count;
        }
    }
}