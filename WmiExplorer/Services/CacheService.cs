using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WmiExplorer.Core.Cache;

namespace WmiExplorer.Services
{
    /// <summary>
    /// Service for managing WMI class metadata cache, persisted to disk.
    /// </summary>
    public class CacheService : ICacheService
    {
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WmiExplorer",
            "Cache.json");
        private static readonly TimeSpan Expiration = TimeSpan.FromDays(7);
        private List<WmiNamespaceCache>? _cache;
        private readonly object _lock = new();

        public async Task<List<WmiNamespaceCache>> LoadCacheAsync()
        {
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;
            }
            if (!File.Exists(CacheFilePath))
            {
                lock (_lock) { _cache = new List<WmiNamespaceCache>(); }
                return _cache;
            }
            try
            {
                var json = await File.ReadAllTextAsync(CacheFilePath).ConfigureAwait(false);
                var cache = JsonSerializer.Deserialize<List<WmiNamespaceCache>>(json) ?? new List<WmiNamespaceCache>();
                lock (_lock) { _cache = cache; }
                return cache;
            }
            catch
            {
                lock (_lock) { _cache = new List<WmiNamespaceCache>(); }
                return _cache;
            }
        }

        public async Task SaveCacheAsync(List<WmiNamespaceCache> cache)
        {
            var dir = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(CacheFilePath, json).ConfigureAwait(false);
            lock (_lock) { _cache = cache; }
        }

        public async Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath)
        {
            var cache = await LoadCacheAsync().ConfigureAwait(false);
            var entry = cache.FirstOrDefault(x => string.Equals(x.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return null;
            if (entry.LastUpdatedUtc.Add(Expiration) < DateTime.UtcNow)
                return null;
            return entry;
        }

        public async Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
        {
            var cache = await LoadCacheAsync().ConfigureAwait(false);
            var existing = cache.FirstOrDefault(x => string.Equals(x.NamespacePath, namespaceCache.NamespacePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                cache.Remove(existing);
            namespaceCache.LastUpdatedUtc = DateTime.UtcNow;
            // Sort: system classes first, then by name
            namespaceCache.Classes = namespaceCache.Classes
                .OrderByDescending(c => c.IsSystemClass)
                .ThenBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            cache.Add(namespaceCache);
            await SaveCacheAsync(cache).ConfigureAwait(false);
        }
    }
}
