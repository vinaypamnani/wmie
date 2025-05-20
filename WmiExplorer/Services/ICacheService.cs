using System.Collections.Generic;
using System.Threading.Tasks;
using WmiExplorer.Core.Cache;

namespace WmiExplorer.Services
{
    /// <summary>
    /// Interface for cache service handling WMI class metadata caching.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Loads the cache from persistent storage.
        /// </summary>
        Task<List<WmiNamespaceCache>> LoadCacheAsync();

        /// <summary>
        /// Saves the cache to persistent storage.
        /// </summary>
        Task SaveCacheAsync(List<WmiNamespaceCache> cache);

        /// <summary>
        /// Gets the cached entry for a specific namespace, or null if not found or expired.
        /// </summary>
        Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath);

        /// <summary>
        /// Updates or adds the cache entry for a namespace.
        /// </summary>
        Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache);
    }
}
