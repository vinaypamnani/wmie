using Microsoft.Data.Sqlite;
using System.IO;
using System.Management;
using WmiExplorer.Core.Cache;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing WMI class metadata cache, persisted to disk.
/// </summary>
public class CacheService : ICacheService
{
    private const int CurrentSchemaVersion = 1;

    private readonly object _lock = new();
    private Dictionary<string, WmiNamespaceCache>? _memoryCache;

    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WmiExplorer",
        "Cache.db");

    private static readonly TimeSpan Expiration = TimeSpan.FromDays(7);

    public CacheService()
    {
        EnsureDatabase();
    }

    /// <summary>
    /// Gets all class metadata for a given namespace, or an empty list if not found or expired.
    /// </summary>
    public async Task<List<WmiClassCache>> GetClassesForNamespaceAsync(string namespacePath)
    {
        var nsCache = await GetNamespaceCacheAsync(namespacePath).ConfigureAwait(false);
        if (nsCache != null && nsCache.Classes != null)
            return nsCache.Classes;

        return new List<WmiClassCache>();
    }

    public async Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath)
    {
        try
        {
            if (_memoryCache == null)
                await LoadCacheAsync().ConfigureAwait(false);

            lock (_lock)
            {
                if (_memoryCache == null)
                    return null;

                if (_memoryCache.TryGetValue(namespacePath, out var entry))
                {
                    if (entry.LastUpdatedUtc.Add(Expiration) < DateTime.UtcNow)
                        return null;

                    return entry;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error getting namespace cache for '{namespacePath}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Gets all property metadata for a given class in a namespace, or an empty list if not found.
    /// </summary>
    public async Task<List<WmiPropertyCache>> GetPropertiesForClassAsync(string namespacePath, string className)
    {
        var nsCache = await GetNamespaceCacheAsync(namespacePath).ConfigureAwait(false);
        if (nsCache != null && nsCache.Classes != null)
        {
            var classCache = nsCache.Classes
                .FirstOrDefault(c => string.Equals(c.ClassName, className, StringComparison.OrdinalIgnoreCase));

            if (classCache != null && classCache.Properties != null)
                return classCache.Properties;
        }

        return new List<WmiPropertyCache>();
    }

    public async Task<List<WmiNamespaceCache>> LoadCacheAsync()
    {
        try
        {
            // Prune expired cache entries in the background to avoid blocking UI
            PruneExpiredCacheInBackground();

            lock (_lock)
            {
                if (_memoryCache != null)
                    return _memoryCache.Values.ToList();
            }

            var result = new Dictionary<string, WmiNamespaceCache>(StringComparer.OrdinalIgnoreCase);
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
                        classCmd.CommandText = "SELECT ClassId, ClassName, IsSystemClass, IsEventClass FROM Classes WHERE NamespaceId = @nsId";
                        classCmd.Parameters.AddWithValue("@nsId", nsId);
                        using var classReader = await classCmd.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await classReader.ReadAsync().ConfigureAwait(false))
                        {
                            var classId = classReader.GetInt32(0);
                            var className = classReader.GetString(1);
                            var isSystem = classReader.GetInt32(2) != 0;
                            var isEvent = classReader.GetInt32(3) != 0;
                            var classCache = new WmiClassCache
                            {
                                ClassName = className,
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
                    result[nsCache.NamespacePath] = nsCache;
                }
            }
            lock (_lock) { _memoryCache = result; }
            return result.Values.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error loading cache: {ex}");
            lock (_lock) { _memoryCache = new Dictionary<string, WmiNamespaceCache>(StringComparer.OrdinalIgnoreCase); }
            return _memoryCache.Values.ToList();
        }
    }

    public async Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            using var tx = conn.BeginTransaction();

            // Upsert namespace and get its ID
            long nsId = await UpsertNamespaceAsync(conn, namespaceCache);

            // Remove classes not present in the incoming cache
            await RemoveObsoleteClassesAsync(conn, nsId, namespaceCache.Classes.Select(c => c.ClassName));

            // Upsert classes and their properties
            foreach (var classCache in namespaceCache.Classes)
            {
                long classId = await UpsertClassAsync(conn, nsId, classCache);

                // Remove properties not present in the incoming cache
                await RemoveObsoletePropertiesAsync(conn, classId, classCache.Properties.Select(p => p.Name));

                // Upsert properties
                foreach (var prop in classCache.Properties)
                {
                    await UpsertPropertyAsync(conn, classId, prop);
                }
            }

            tx.Commit();

            // Update in-memory cache efficiently
            lock (_lock)
            {
                if (_memoryCache != null)
                {
                    // Simply update or add the entry in the dictionary
                    _memoryCache[namespaceCache.NamespacePath] = namespaceCache;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheService] Error updating namespace cache for '{namespaceCache.NamespacePath}': {ex}");
        }
    }

    private void CreateDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Create all tables, including SchemaVersion
        cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS SchemaVersion (
        Id INTEGER PRIMARY KEY CHECK (Id = 1),
        Version INTEGER NOT NULL
    );
    CREATE TABLE IF NOT EXISTS Namespaces (
        NamespaceId INTEGER PRIMARY KEY AUTOINCREMENT,
        NamespacePath TEXT UNIQUE,
        LastUpdatedUtc TEXT
    );
    CREATE TABLE IF NOT EXISTS Classes (
        ClassId INTEGER PRIMARY KEY AUTOINCREMENT,
        NamespaceId INTEGER,
        ClassName TEXT,
        IsSystemClass INTEGER,
        IsEventClass INTEGER,
        FOREIGN KEY(NamespaceId) REFERENCES Namespaces(NamespaceId) ON DELETE CASCADE
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

        // Set schema version after creating tables - ensuring we only have one row
        cmd.CommandText = "INSERT OR REPLACE INTO SchemaVersion (Id, Version) VALUES (1, @ver)";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@ver", CurrentSchemaVersion);
        cmd.ExecuteNonQuery();
    }

    private void EnsureDatabase()
    {
        var dir = Path.GetDirectoryName(CacheFilePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        bool recreate = false;
        SqliteConnection? conn = null;

        try
        {
            // Only open the connection to check the schema version, not before
            conn = new SqliteConnection($"Data Source={CacheFilePath}");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
                var exists = cmd.ExecuteScalar() != null;

                int dbVersion = 0;
                if (exists)
                {
                    cmd.CommandText = "SELECT Version FROM SchemaVersion";
                    var result = cmd.ExecuteScalar();
                    dbVersion = result != null ? Convert.ToInt32(result) : 0;
                }

                if (!exists || dbVersion != CurrentSchemaVersion)
                {
                    recreate = true;
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Schema version mismatch: expected {CurrentSchemaVersion}, found {dbVersion}. Recreating database.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CacheService] Schema version is up-to-date: {dbVersion}.");
                }
            }
        }
        catch
        {
            recreate = true;
        }
        finally
        {
            // Ensure we close the connection if it was opened
            conn?.Close();
            if (conn != null)
            {
                SqliteConnection.ClearPool(conn); // Ensure all pooled connections are released
            }

            // Dispose the connection to release any resources
            conn?.Dispose();
        }

        // Only delete the file when no connection is open
        if (recreate)
        {
            if (File.Exists(CacheFilePath))
            {
                // Force garbage collection to clean up any unmanaged connections
                GC.Collect();
                GC.WaitForPendingFinalizers();

                bool deleted = false;
                // Wait for a short time in case another process is using the file
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(CacheFilePath);
                        System.Diagnostics.Debug.WriteLine("[CacheService] Cache.db deleted successfully.");
                        deleted = true;
                        break;
                    }
                    catch (IOException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CacheService] Attempt {i + 1}/5 to delete Cache.db failed (file in use): {ex.Message}");
                        System.Threading.Thread.Sleep(500); // Increased wait time
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CacheService] Attempt {i + 1}/5 to delete Cache.db failed: {ex.Message}");
                        System.Threading.Thread.Sleep(300); // Increased wait time
                    }
                }

                if (!deleted)
                {
                    // If we couldn't delete after retries, log the warning but continue
                    // We'll overwrite the file when creating the new database
                    System.Diagnostics.Debug.WriteLine("[CacheService] Warning: Could not delete Cache.db after multiple attempts.");
                }
                else
                {
                    // If we successfully deleted, we can recreate the database
                    CreateDatabase();
                }
            }
        }
        else
        {
            CreateDatabase();
        }
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

            // Select expired namespace IDs and paths together
            var expiredNamespaces = new Dictionary<long, string>();
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = @"
                SELECT NamespaceId, NamespacePath FROM Namespaces
                WHERE datetime(LastUpdatedUtc) < datetime('now', @expiration)";
                selectCmd.Parameters.AddWithValue("@expiration", $"-{Expiration.TotalDays} days");

                using var reader = await selectCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var nsId = reader.GetInt64(0);
                    var nsPath = reader.GetString(1);
                    expiredNamespaces.Add(nsId, nsPath);
                }
            }

            if (expiredNamespaces.Count == 0)
                return; // Nothing to do

            // Now delete the data with a transaction
            using var tx = conn.BeginTransaction();

            foreach (var nsId in expiredNamespaces.Keys)
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

            // Now update the in-memory cache with the paths we saved earlier
            bool removed = false;
            lock (_lock)
            {
                if (_memoryCache != null)
                {
                    foreach (var nsPath in expiredNamespaces.Values)
                    {
                        if (_memoryCache.Remove(nsPath))
                            removed = true;
                    }
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

    /// <summary>
    /// Removes classes from the database that are not present in the provided class names.
    /// </summary>
    private static async Task RemoveObsoleteClassesAsync(SqliteConnection conn, long nsId, IEnumerable<string> classNames)
    {
        var classNameList = classNames.ToList();
        if (classNameList.Count == 0)
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM Classes WHERE NamespaceId = @nsId";
            delCmd.Parameters.AddWithValue("@nsId", nsId);
            await delCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return;
        }

        using var delClassCmd = conn.CreateCommand();
        delClassCmd.CommandText = $"DELETE FROM Classes WHERE NamespaceId = @nsId AND ClassName NOT IN ({string.Join(",", classNameList.Select((_, i) => $"@cn{i}"))})";
        delClassCmd.Parameters.AddWithValue("@nsId", nsId);
        for (int i = 0; i < classNameList.Count; i++)
            delClassCmd.Parameters.AddWithValue($"@cn{i}", classNameList[i]);
        await delClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Removes properties from the database that are not present in the provided property names.
    /// </summary>
    private static async Task RemoveObsoletePropertiesAsync(SqliteConnection conn, long classId, IEnumerable<string> propertyNames)
    {
        var propNameList = propertyNames.ToList();
        if (propNameList.Count == 0)
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM ClassProperties WHERE ClassId = @classId";
            delCmd.Parameters.AddWithValue("@classId", classId);
            await delCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return;
        }

        using var delPropCmd = conn.CreateCommand();
        delPropCmd.CommandText = $"DELETE FROM ClassProperties WHERE ClassId = @classId AND PropertyName NOT IN ({string.Join(",", propNameList.Select((_, i) => $"@pn{i}"))})";
        delPropCmd.Parameters.AddWithValue("@classId", classId);
        for (int i = 0; i < propNameList.Count; i++)
            delPropCmd.Parameters.AddWithValue($"@pn{i}", propNameList[i]);
        await delPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Upserts a class and returns its ID.
    /// </summary>
    private static async Task<long> UpsertClassAsync(SqliteConnection conn, long nsId, WmiClassCache classCache)
    {
        // Try update first
        using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.CommandText = @"UPDATE Classes SET IsSystemClass = @sys, IsEventClass = @evt
                                      WHERE NamespaceId = @nsId AND ClassName = @cn";
            updateCmd.Parameters.AddWithValue("@sys", classCache.IsSystemClass ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@evt", classCache.IsEventClass ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@nsId", nsId);
            updateCmd.Parameters.AddWithValue("@cn", classCache.ClassName);
            int rows = await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
            {
                // Get ClassId
                using var getCmd = conn.CreateCommand();
                getCmd.CommandText = "SELECT ClassId FROM Classes WHERE NamespaceId = @nsId AND ClassName = @cn";
                getCmd.Parameters.AddWithValue("@nsId", nsId);
                getCmd.Parameters.AddWithValue("@cn", classCache.ClassName);
                return (long)(await getCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
        }

        // Insert if not exists
        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = @"INSERT INTO Classes (NamespaceId, ClassName, IsSystemClass, IsEventClass)
                                   VALUES (@nsId, @cn, @sys, @evt);";
            insCmd.Parameters.AddWithValue("@nsId", nsId);
            insCmd.Parameters.AddWithValue("@cn", classCache.ClassName);
            insCmd.Parameters.AddWithValue("@sys", classCache.IsSystemClass ? 1 : 0);
            insCmd.Parameters.AddWithValue("@evt", classCache.IsEventClass ? 1 : 0);
            await insCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            insCmd.CommandText = "SELECT ClassId FROM Classes WHERE NamespaceId = @nsId AND ClassName = @cn";
            return (long)(await insCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
        }
    }

    /// <summary>
    /// Upserts a namespace and returns its ID.
    /// </summary>
    private static async Task<long> UpsertNamespaceAsync(SqliteConnection conn, WmiNamespaceCache namespaceCache)
    {
        using var nsCmd = conn.CreateCommand();
        nsCmd.CommandText = @"INSERT INTO Namespaces (NamespacePath, LastUpdatedUtc) VALUES (@ns, @dt)
                              ON CONFLICT(NamespacePath) DO UPDATE SET LastUpdatedUtc = excluded.LastUpdatedUtc;";
        nsCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
        nsCmd.Parameters.AddWithValue("@dt", namespaceCache.LastUpdatedUtc.ToString("o"));
        await nsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        nsCmd.CommandText = "SELECT NamespaceId FROM Namespaces WHERE NamespacePath = @ns";
        return (long)(await nsCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    /// <summary>
    /// Upserts a property for a class.
    /// </summary>
    private static async Task UpsertPropertyAsync(SqliteConnection conn, long classId, WmiPropertyCache prop)
    {
        // Try update first
        using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.CommandText = @"UPDATE ClassProperties SET PropertyType = @pt
                                      WHERE ClassId = @classId AND PropertyName = @pn";
            updateCmd.Parameters.AddWithValue("@pt", prop.Type);
            updateCmd.Parameters.AddWithValue("@classId", classId);
            updateCmd.Parameters.AddWithValue("@pn", prop.Name);
            int rows = await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
                return;
        }

        // Insert if not exists
        using var insCmd = conn.CreateCommand();
        insCmd.CommandText = @"INSERT INTO ClassProperties (ClassId, PropertyName, PropertyType)
                               VALUES (@classId, @pn, @pt)";
        insCmd.Parameters.AddWithValue("@classId", classId);
        insCmd.Parameters.AddWithValue("@pn", prop.Name);
        insCmd.Parameters.AddWithValue("@pt", prop.Type);
        await insCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}