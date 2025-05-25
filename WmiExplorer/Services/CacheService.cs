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
                cache = _cache;

            if (cache == null)
                cache = await LoadCacheAsync().ConfigureAwait(false);

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
            // Prune expired cache entries in the background to avoid blocking UI
            PruneExpiredCacheInBackground();
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;
            }
            var result = new List<WmiNamespaceCache>();
            using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            // Load namespaces
            using (var nsCmd = conn.CreateCommand())
            {
                nsCmd.CommandText = "SELECT NamespaceId, NamespacePath, LastUpdatedUtc FROM Namespaces";
                using var reader = await nsCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var nsId = reader.GetInt32(0);
                    var nsPath = reader.GetString(1);
                    var lastUpdated = DateTime.Parse(reader.GetString(2));
                    var nsCache = new WmiNamespaceCache
                    {
                        NamespacePath = nsPath,
                        LastUpdatedUtc = lastUpdated,
                        Classes = new List<WmiClassCache>()
                    };
                    // Load classes for this namespace
                    using (var classCmd = conn.CreateCommand())
                    {
                        classCmd.CommandText = "SELECT ClassId, ClassName, RelativePath, IsSystemClass, IsEventClass FROM Classes WHERE NamespaceId = @nsId";
                        classCmd.Parameters.AddWithValue("@nsId", nsId);
                        using var classReader = await classCmd.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await classReader.ReadAsync().ConfigureAwait(false))
                        {
                            var classId = classReader.GetInt32(0);
                            var className = classReader.GetString(1);
                            var relPath = classReader.GetString(2);
                            var isSystem = classReader.GetInt32(3) != 0;
                            var isEvent = classReader.GetInt32(4) != 0;
                            var classCache = new WmiClassCache
                            {
                                ClassName = className,
                                RelativePath = relPath,
                                IsSystemClass = isSystem,
                                IsEventClass = isEvent,
                                Properties = new List<WmiPropertyCache>()
                            };
                            // Load properties for this class
                            using (var propCmd = conn.CreateCommand())
                            {
                                propCmd.CommandText = "SELECT PropertyName, PropertyType FROM ClassProperties WHERE ClassId = @classId";
                                propCmd.Parameters.AddWithValue("@classId", classId);
                                using var propReader = await propCmd.ExecuteReaderAsync().ConfigureAwait(false);
                                while (await propReader.ReadAsync().ConfigureAwait(false))
                                {
                                    classCache.Properties.Add(new WmiPropertyCache
                                    {
                                        Name = propReader.GetString(0),
                                        Type = propReader.GetString(1)
                                    });
                                }
                            }
                            nsCache.Classes.Add(classCache);
                        }
                    }
                    result.Add(nsCache);
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
            long nsId;
            using (var nsCmd = conn.CreateCommand())
            {
                nsCmd.CommandText = @"INSERT INTO Namespaces (NamespacePath, LastUpdatedUtc) VALUES (@ns, @dt)
                        ON CONFLICT(NamespacePath) DO UPDATE SET LastUpdatedUtc = excluded.LastUpdatedUtc;";
                nsCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
                nsCmd.Parameters.AddWithValue("@dt", namespaceCache.LastUpdatedUtc.ToString("o"));
                await nsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                // Get NamespaceId
                nsCmd.CommandText = "SELECT NamespaceId FROM Namespaces WHERE NamespacePath = @ns";
                nsId = (long)(await nsCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
            // Remove old classes for this namespace
            using (var delClassCmd = conn.CreateCommand())
            {
                delClassCmd.CommandText = "DELETE FROM Classes WHERE NamespaceId = @nsId";
                delClassCmd.Parameters.AddWithValue("@nsId", nsId);
                await delClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            // Insert new classes and properties
            foreach (var c in namespaceCache.Classes)
            {
                long classId;
                using (var insClassCmd = conn.CreateCommand())
                {
                    insClassCmd.CommandText = @"INSERT INTO Classes (NamespaceId, ClassName, RelativePath, IsSystemClass, IsEventClass)
                            VALUES (@nsId, @cn, @rp, @sys, @evt);";
                    insClassCmd.Parameters.AddWithValue("@nsId", nsId);
                    insClassCmd.Parameters.AddWithValue("@cn", c.ClassName);
                    insClassCmd.Parameters.AddWithValue("@rp", c.RelativePath);
                    insClassCmd.Parameters.AddWithValue("@sys", c.IsSystemClass ? 1 : 0);
                    insClassCmd.Parameters.AddWithValue("@evt", c.IsEventClass ? 1 : 0);
                    await insClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    // Get ClassId
                    insClassCmd.CommandText = "SELECT ClassId FROM Classes WHERE NamespaceId = @nsId AND ClassName = @cn";
                    classId = (long)(await insClassCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
                }
                // Remove old properties for this class
                using (var delPropCmd = conn.CreateCommand())
                {
                    delPropCmd.CommandText = "DELETE FROM ClassProperties WHERE ClassId = @classId";
                    delPropCmd.Parameters.AddWithValue("@classId", classId);
                    await delPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                // Insert new properties
                foreach (var p in c.Properties)
                {
                    using var insPropCmd = conn.CreateCommand();
                    insPropCmd.CommandText = @"INSERT INTO ClassProperties (ClassId, PropertyName, PropertyType)
                                               VALUES (@classId, @pn, @pt)";
                    insPropCmd.Parameters.AddWithValue("@classId", classId);
                    insPropCmd.Parameters.AddWithValue("@pn", p.Name);
                    insPropCmd.Parameters.AddWithValue("@pt", p.Type);
                    await insPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
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

        bool retry = false;
        try
        {
            CreateDatabase();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error ensuring database: {ex}. Attempting to delete and recreate cache.db.");
            retry = true;
        }

        if (retry)
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);
                CreateDatabase();
            }
            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine($"[CacheService] Failed to recreate cache.db: {ex2}");
                throw;
            }
        }
    }

    private void CreateDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Drop and recreate ClassProperties table to ensure ON DELETE CASCADE is set
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Namespaces (
            NamespaceId INTEGER PRIMARY KEY AUTOINCREMENT,
            NamespacePath TEXT UNIQUE,
            LastUpdatedUtc TEXT
        );
        CREATE TABLE IF NOT EXISTS Classes (
            ClassId INTEGER PRIMARY KEY AUTOINCREMENT,
            NamespaceId INTEGER,
            ClassName TEXT,
            RelativePath TEXT,
            IsSystemClass INTEGER,
            IsEventClass INTEGER,
            FOREIGN KEY(NamespaceId) REFERENCES Namespaces(NamespaceId)
        );
        CREATE INDEX IF NOT EXISTS idx_Classes_NamespaceId ON Classes(NamespaceId);
        CREATE TABLE IF NOT EXISTS ClassProperties (
            PropertyId INTEGER PRIMARY KEY AUTOINCREMENT,
            ClassId INTEGER,
            PropertyName TEXT,
            PropertyType TEXT,
            FOREIGN KEY(ClassId) REFERENCES Classes(ClassId) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS idx_ClassProperties_ClassId ON ClassProperties(ClassId);
    ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Asynchronously removes expired namespace cache entries and their related data from the database.
    /// </summary>
    private async Task PruneExpiredCacheAsync()
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            using var tx = conn.BeginTransaction();

            // Select expired namespace IDs
            var expiredNamespaceIds = new List<long>();
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = @"
                    SELECT NamespaceId FROM Namespaces
                    WHERE datetime(LastUpdatedUtc) < datetime('now', @expiration)";
                selectCmd.Parameters.AddWithValue("@expiration", $"-{Expiration.TotalDays} days");

                using var reader = await selectCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    expiredNamespaceIds.Add(reader.GetInt64(0));
                }
            }

            foreach (var nsId in expiredNamespaceIds)
            {
                // Delete properties for all classes in this namespace
                using (var delPropCmd = conn.CreateCommand())
                {
                    delPropCmd.CommandText = @"
                        DELETE FROM ClassProperties WHERE ClassId IN
                        (SELECT ClassId FROM Classes WHERE NamespaceId = @nsId)";
                    delPropCmd.Parameters.AddWithValue("@nsId", nsId);
                    await delPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                // Delete classes for this namespace
                using (var delClassCmd = conn.CreateCommand())
                {
                    delClassCmd.CommandText = "DELETE FROM Classes WHERE NamespaceId = @nsId";
                    delClassCmd.Parameters.AddWithValue("@nsId", nsId);
                    await delClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                // Delete the namespace itself
                using (var delNsCmd = conn.CreateCommand())
                {
                    delNsCmd.CommandText = "DELETE FROM Namespaces WHERE NamespaceId = @nsId";
                    delNsCmd.Parameters.AddWithValue("@nsId", nsId);
                    await delNsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }

            tx.Commit();

            // Also prune from in-memory cache if loaded
            bool removed = false;
            lock (_lock)
            {
                if (_cache != null && expiredNamespaceIds.Count > 0)
                {
                    _cache.RemoveAll(ns => expiredNamespaceIds.Any()); // Remove all if any expired, since NamespaceId is not tracked in WmiNamespaceCache
                    removed = true;
                }
            }

            // Only reclaim disk space by running VACUUM if something was actually removed
            if (removed)
            {
                try
                {
                    using var vacuumCmd = conn.CreateCommand();
                    vacuumCmd.CommandText = "VACUUM;";
                    await vacuumCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                catch (Exception exVacuum)
                {
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Error running VACUUM: {exVacuum}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error pruning expired cache: {ex}");
        }
    }

    /// <summary>
    /// Asynchronously removes expired namespace cache entries and their related data from the database in a background thread.
    /// </summary>
    private void PruneExpiredCacheInBackground()
    {
        // Fire and forget background task for pruning
        Task.Run(async () =>
        {
            try
            {
                await PruneExpiredCacheAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CacheService] Error in background pruning: {ex}");
            }
        });
    }
}