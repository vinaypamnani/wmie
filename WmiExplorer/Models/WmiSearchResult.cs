using System.Management;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Models;

/// <summary>
/// Represents a unified search result for WMI search (Class, Method, or Property).
/// </summary>
public class WmiSearchResult
{
    public WmiSearchResult(WmiSearchType searchType, object match, ManagementBaseObject parent)
    {
        SearchType = searchType;
        Match = match ?? throw new ArgumentNullException(nameof(match));
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));

        // Create appropriate model objects based on search type and match object
        switch (searchType)
        {
            case WmiSearchType.Class when match is ManagementClass managementClass:
                Class = new WmiClass(managementClass);
                NamespacePath = Class.Scope?.Path?.Path ?? string.Empty; // Use full path (\\machine\root\cimv2)
                break;
            case WmiSearchType.Method when match is MethodData methodData && parent is ManagementClass parentClass:
                Method = new WmiMethod(methodData, parentClass);
                NamespacePath = parentClass.Scope?.Path?.Path ?? string.Empty;
                break;
            case WmiSearchType.Property when match is PropertyData propertyData && parent is ManagementClass parentClass:
                Property = new WmiProperty(propertyData, parentClass);
                NamespacePath = parentClass.Scope?.Path?.Path ?? string.Empty;
                break;
            default:
                NamespacePath = string.Empty;
                break;
        }
    }

    public WmiClass? Class { get; }

    public string Description => SearchType switch
    {
        WmiSearchType.Class => Class?.Description ?? string.Empty,
        WmiSearchType.Method => Method?.Description ?? string.Empty,
        WmiSearchType.Property => Property?.Description ?? string.Empty,
        _ => string.Empty
    };

    public object Match { get; }
    public WmiMethod? Method { get; }

    // Unified properties for simplified binding
    public string Name => SearchType switch
    {
        WmiSearchType.Class => Class?.ClassName ?? "Unknown",
        WmiSearchType.Method => Method?.Name ?? (Match is MethodData md ? md.Name : "Unknown"),
        WmiSearchType.Property => Property?.Name ?? (Match is PropertyData pd ? pd.Name : "Unknown"),
        _ => "Unknown"
    };

    public string NamespacePath { get; }
    public ManagementBaseObject Parent { get; }

    public string Path => SearchType switch
    {
        WmiSearchType.Class => Class?.ClassPath.RelativePath ?? string.Empty,
        WmiSearchType.Method => Method != null ? $"{Method.ClassName}.{Method.Name}" : string.Empty,
        WmiSearchType.Property => Property != null ? $"{Property.ClassName}.{Property.Name}" : string.Empty,
        _ => string.Empty
    };

    public WmiProperty? Property { get; }
    public WmiSearchType SearchType { get; }

    public string? TypeInfo => SearchType switch
    {
        WmiSearchType.Property => Property?.Type ?? string.Empty,
        _ => null
    };

    public override string ToString()
    {
        return "Search Result: " + Name + " (" + SearchType + ")";
    }
}