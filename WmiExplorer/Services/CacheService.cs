using Microsoft.Data.Sqlite;
using System.IO;
using WmiExplorer.Core.Cache;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing WMI class metadata cache, persisted to disk.
/// </summary>
public class CacheService : ICacheService
{
    private List<WmiNamespaceCache>? _cache;
    private readonly object _lock = new();

    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WmiExplorer",
        "Cache.db");

    private static readonly TimeSpan Expiration = TimeSpan.FromDays(7);

    public CacheService()
    {
        EnsureDatabase();
    }

    public async Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath)
    {
        try
        {
            List<WmiNamespaceCache>? cache;
            lock (_lock)
            {
                cache = _cache;
            }
            if (cache == null)
            {
                cache = await LoadCacheAsync().ConfigureAwait(false);
            }
            var entry = cache.FirstOrDefault(x => string.Equals(x.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return null;
            if (entry.LastUpdatedUtc.Add(Expiration) < DateTime.UtcNow)
                return null;
            return entry;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error getting namespace cache for '{namespacePath}': {ex}");
            return null;
        }
    }

    public async Task<List<WmiNamespaceCache>> LoadCacheAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;
            }
            var result = new List<WmiNamespaceCache>();
            using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            using (var nsCmd = conn.CreateCommand())
            {
                nsCmd.CommandText = "SELECT NamespacePath, LastUpdatedUtc FROM Namespaces";
                using var reader = await nsCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var nsPath = reader.GetString(0);
                    var lastUpdated = DateTime.Parse(reader.GetString(1));
                    var nsCache = new WmiNamespaceCache
                    {
                        NamespacePath = nsPath,
                        LastUpdatedUtc = lastUpdated,
                        Classes = new List<WmiClassCache>()
                    };
                    result.Add(nsCache);
                }
            }
            // Load classes for each namespace
            foreach (var ns in result)
            {
                using var classCmd = conn.CreateCommand();
                classCmd.CommandText = "SELECT ClassName, RelativePath, IsSystemClass, IsEventClass, PropertyNames FROM Classes WHERE NamespacePath = @ns";
                classCmd.Parameters.AddWithValue("@ns", ns.NamespacePath);
                using var classReader = await classCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await classReader.ReadAsync().ConfigureAwait(false))
                {
                    var className = classReader.GetString(0);
                    var relPath = classReader.GetString(1);
                    var isSystem = classReader.GetInt32(2) != 0;
                    var isEvent = classReader.GetInt32(3) != 0;
                    var propNames = classReader.GetString(4);
                    var propList = propNames.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                    ns.Classes.Add(new WmiClassCache
                    {
                        ClassName = className,
                        RelativePath = relPath,
                        IsSystemClass = isSystem,
                        IsEventClass = isEvent,
                        PropertyNames = propList
                    });
                }
            }
            lock (_lock) { _cache = result; }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error loading cache: {ex}");
            lock (_lock) { _cache = new List<WmiNamespaceCache>(); }
            return _cache;
        }
    }

    public async Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            using var tx = conn.BeginTransaction();
            // Upsert namespace
            using (var nsCmd = conn.CreateCommand())
            {
                nsCmd.CommandText = @"INSERT INTO Namespaces (NamespacePath, LastUpdatedUtc) VALUES (@ns, @dt)
                        ON CONFLICT(NamespacePath) DO UPDATE SET LastUpdatedUtc = excluded.LastUpdatedUtc";
                nsCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
                nsCmd.Parameters.AddWithValue("@dt", namespaceCache.LastUpdatedUtc.ToString("o"));
                await nsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            // Remove old classes for this namespace
            using (var delCmd = conn.CreateCommand())
            {
                delCmd.CommandText = "DELETE FROM Classes WHERE NamespacePath = @ns";
                delCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
                await delCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            // Insert new classes
            foreach (var c in namespaceCache.Classes)
            {
                using var insCmd = conn.CreateCommand();
                insCmd.CommandText = @"INSERT INTO Classes (NamespacePath, ClassName, RelativePath, IsSystemClass, IsEventClass, PropertyNames)
                        VALUES (@ns, @cn, @rp, @sys, @evt, @props)";
                insCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
                insCmd.Parameters.AddWithValue("@cn", c.ClassName);
                insCmd.Parameters.AddWithValue("@rp", c.RelativePath);
                insCmd.Parameters.AddWithValue("@sys", c.IsSystemClass ? 1 : 0);
                insCmd.Parameters.AddWithValue("@evt", c.IsEventClass ? 1 : 0);
                insCmd.Parameters.AddWithValue("@props", string.Join(';', c.PropertyNames ?? new List<string>()));
                await insCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            tx.Commit();
            // Update in-memory cache efficiently: update or add the namespace in _cache
            lock (_lock)
            {
                if (_cache != null)
                {
                    var existing = _cache.FirstOrDefault(x => string.Equals(x.NamespacePath, namespaceCache.NamespacePath, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        _cache.Remove(existing);
                    _cache.Add(namespaceCache);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error updating namespace cache for '{namespaceCache.NamespacePath}': {ex}");
        }
    }

    private void EnsureDatabase()
    {
        var dir = Path.GetDirectoryName(CacheFilePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);
        using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Namespaces (
                    NamespacePath TEXT PRIMARY KEY,
                    LastUpdatedUtc TEXT
                );
                CREATE TABLE IF NOT EXISTS Classes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    NamespacePath TEXT,
                    ClassName TEXT,
                    RelativePath TEXT,
                    IsSystemClass INTEGER,
                    IsEventClass INTEGER,
                    PropertyNames TEXT,
                    FOREIGN KEY(NamespacePath) REFERENCES Namespaces(NamespacePath)
                );
                CREATE INDEX IF NOT EXISTS idx_Classes_NamespacePath ON Classes(NamespacePath);
            ";
        cmd.ExecuteNonQuery();
    }
}