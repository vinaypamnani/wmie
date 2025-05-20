using System;

namespace WmiExplorer.Core.Cache
{
    /// <summary>
    /// Represents lightweight cached metadata for a WMI class.
    /// </summary>
    public class WmiClassCache
    {
        public string ClassName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsSystemClass { get; set; }
        /// <summary>
        /// True if the class is an event class (derives from __Event).
        /// </summary>
        public bool IsEventClass { get; set; }
    }
}
