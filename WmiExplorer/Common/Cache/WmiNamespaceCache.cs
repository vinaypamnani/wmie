namespace WmiExplorer.Common.Cache;

/// <summary>
/// Represents cached metadata for all classes in a WMI namespace.
/// </summary>
public class WmiNamespaceCache
{
    /// <summary>
    /// The list of cached class metadata for this namespace.
    /// </summary>
    public List<WmiClassCache> Classes { get; set; } = new List<WmiClassCache>();

    /// <summary>
    /// The UTC timestamp when this cache entry was last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; }

    /// <summary>
    /// The full WMI namespace path (e.g., "root\\cimv2").
    /// </summary>
    public string NamespacePath { get; set; } = string.Empty;
}