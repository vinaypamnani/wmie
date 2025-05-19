using System;

namespace WmiExplorer.Core.Models
{
    /// <summary>
    /// Model for WMI Event Watcher
    /// </summary>
    public class WmiEventModel
    {
        /// <summary>
        /// Gets or sets the WMI Query for watching events
        /// </summary>
        public string? Query { get; set; }

        /// <summary>
        /// Gets or sets the namespace to watch
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Gets or sets the event type to watch
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Gets or sets whether the watcher is active
        /// </summary>
        public bool IsActive { get; set; }
    }
}
