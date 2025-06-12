using System.ComponentModel;
using System.Management;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Represents a WMI event received from a watcher.
/// </summary>
public class WmiEvent : IDisposable
{
    public WmiEvent(string watcherName, ManagementBaseObject eventData, string eventDisplayPropertyName)
    {
        WatcherName = watcherName ?? throw new ArgumentNullException(nameof(watcherName));
        EventDisplayPropertyName = eventDisplayPropertyName ?? string.Empty;
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
            System.Diagnostics.Debug.WriteLine($"[WmiEvent] Embedded instance access error: {ex.Message}");
        }

        // Try to use the requested display property, fallback to __RELPATH
        string displayValue = string.Empty;
        if (!string.IsNullOrWhiteSpace(eventDisplayPropertyName))
        {
            displayValue = TryGetPropertyValue(targetObject, eventDisplayPropertyName)
                ?? TryGetPropertyValue(eventData, eventDisplayPropertyName)
                ?? TryGetPropertyValue(targetObject, "__RELPATH")
                ?? "<Unknown>";
        }

        EventDisplayPropertyValue = displayValue;
        EventClassName = eventData.ClassPath?.ClassName ?? "<Unknown>";

        if (targetObject != null && previousObject != null)
        {
            EventChanges = GetPropertyDifferences(previousObject, targetObject);
        }
    }

    [Category("Event")]
    [ShowChildrenAsParent]
    public WmiEventChangeTracker? EventChanges { get; }

    public string EventClassName { get; }

    [Category("Event")]
    [ShowChildrenAsParent]
    public ManagementBaseObject EventData { get; }

    [Category("Event")]
    public string EventDisplayPropertyName { get; }

    [Category("Event")]
    public string EventDisplayPropertyValue { get; }

    [Category("Event")]
    public DateTime EventTimestamp { get; }

    public string WatcherName { get; }

    /// <summary>
    /// Utility to diff two ManagementBaseObject instances.
    /// </summary>
    public static WmiEventChangeTracker GetPropertyDifferences(ManagementBaseObject? previous, ManagementBaseObject? current)
    {
        var diff = new WmiEventChangeTracker();
        if (previous == null || current == null)
            return diff;

        foreach (PropertyData property in current.Properties)
        {
            var propName = property.Name;
            var newValue = property.Value;
            var oldValue = previous.Properties[propName]?.Value;
            if (!Equals(newValue, oldValue))
            {
                diff.ChangedProperties.Add(new WmiEventChangeTracker.DiffEntry
                {
                    PropertyName = propName,
                    OldValue = oldValue,
                    NewValue = newValue
                });
            }
        }
        return diff;
    }

    public override string ToString()
    {
        return $"Event: {EventDisplayPropertyName} = {EventDisplayPropertyValue}";
    }

    private static string? TryGetPropertyValue(ManagementBaseObject? obj, string propertyName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(propertyName))
            return null;
        try
        {
            var val = obj[propertyName];
            return val?.ToString();
        }
        catch
        {
            return null;
        }
    }

    #region IDisposable

    public void Dispose()
    {
        // Dispose the main ManagementBaseObject
        EventData?.Dispose();
    }

    #endregion
}

/// <summary>
/// Represents the difference between two WMI instances.
/// </summary>
public class WmiEventChangeTracker
{
    [ExpandByDefault]
    public List<DiffEntry> ChangedProperties { get; } = new List<DiffEntry>();

    public override string ToString()
    {
        return "Changed Properties: " + ChangedProperties.Count;
    }

    public class DiffEntry
    {
        public object? NewValue { get; set; }
        public object? OldValue { get; set; }
        public string PropertyName { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{PropertyName}: {OldValue} -> {NewValue}";
        }
    }
}