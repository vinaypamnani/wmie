using WmiExplorer.Common.Cache;

namespace WmiExplorer.Services;

/// <summary>
/// Interface for cache service handling WMI class metadata caching.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets the cached entry for a specific namespace, or null if not found or expired.
    /// </summary>
    Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath);

    /// <summary>
    /// Loads the cache from persistent storage.
    /// </summary>
    Task<List<WmiNamespaceCache>> LoadCacheAsync();

    /// <summary>
    /// Updates or adds the cache entry for a namespace.
    /// </summary>
    Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache);

    /// <summary>
    /// Gets all class metadata for a given namespace, or an empty list if not found or expired.
    /// </summary>
    Task<List<WmiClassCache>> GetClassesForNamespaceAsync(string namespacePath);

    /// <summary>
    /// Gets all property metadata for a given class in a namespace, or an empty list if not found.
    /// </summary>
    Task<List<WmiPropertyCache>> GetPropertiesForClassAsync(string namespacePath, string className);
}