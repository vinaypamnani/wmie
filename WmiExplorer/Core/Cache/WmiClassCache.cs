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
    /// List of property names for the WMI class, excluding system properties (those starting with "__").
    /// </summary>
    public List<string> PropertyNames { get; set; } = new List<string>();

    public string RelativePath { get; set; } = string.Empty;
}