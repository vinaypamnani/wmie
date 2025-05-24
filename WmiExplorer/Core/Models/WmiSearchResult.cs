using System.Management;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Core.Models;

/// <summary>
/// Represents a unified search result for WMI search (Class, Method, or Property).
/// </summary>
public class WmiSearchResult
{
    public WmiSearchResult(WmiSearchType searchType, object match, ManagementBaseObject parent)
    {
        SearchType = searchType;
        Match = match ?? throw new ArgumentNullException(nameof(match));
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));        // Create appropriate model objects based on search type and match object
        switch (searchType)
        {
            case WmiSearchType.Class when match is ManagementClass managementClass:
                Class = new WmiClass(managementClass);
                break;

            case WmiSearchType.Method when match is MethodData methodData && parent is ManagementClass parentClass:
                Method = new WmiMethod(methodData, parentClass);
                break;

            case WmiSearchType.Property when match is PropertyData propertyData && parent is ManagementClass parentClass:
                Property = new WmiProperty(propertyData, parentClass);
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

    private static string? ExtractName(object obj)
    {
        try
        {
            // Handle different object types that might have a Name property
            return obj switch
            {
                MethodData method => method.Name,
                PropertyData property => property.Name,
                ManagementBaseObject mbo => mbo["Name"]?.ToString(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}