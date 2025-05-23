namespace WmiExplorer.Core.Cache;

/// <summary>
/// Represents a cached WMI property with its name and type.
/// </summary>
public class WmiPropertyCache
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
