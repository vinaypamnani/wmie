namespace WmiExplorer.Core.Cache;

/// <summary>
/// Represents lightweight cached metadata for a WMI class.
/// </summary>
public class WmiClassCache
{
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// True if the class is an event class (derives from __Event).
    /// </summary>
    public bool IsEventClass { get; set; }

    public bool IsSystemClass { get; set; }

    /// <summary>
    /// List of property name/type pairs for the WMI class.
    /// </summary>
    public List<WmiPropertyCache> Properties { get; set; } = new List<WmiPropertyCache>();
}