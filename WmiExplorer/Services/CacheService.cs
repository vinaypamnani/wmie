using Microsoft.Data.Sqlite;
using System.IO;
using WmiExplorer.Common.Cache;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing WMI class metadata cache, persisted to disk.
/// </summary>
public class CacheService : ICacheService
{
    private const int CurrentSchemaVersion = 2;

    private readonly object _lock = new();
    private Dictionary<string, WmiNamespaceCache>? _memoryCache;

    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WmiExplorer",
        "Cache.db");

    private static readonly TimeSpan Expiration = TimeSpan.FromDays(7);
    private static readonly TimeSpan PruneInterval = TimeSpan.FromDays(45);

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
            Log.Error(ex, "CacheService error getting namespace cache for {NamespacePath}", namespacePath);
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
            await using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);

            // Load namespaces
            await using var nsCmd = conn.CreateCommand();
            nsCmd.CommandText = "SELECT NamespaceId, NamespacePath, LastUpdatedUtc FROM Namespaces WHERE IsExpired = 0";
            await using var reader = await nsCmd.ExecuteReaderAsync().ConfigureAwait(false);

            var namespacesData = new List<(int nsId, string nsPath, DateTime lastUpdated)>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var nsId = reader.GetInt32(0);
                var nsPath = reader.GetString(1);
                var lastUpdated = DateTime.Parse(reader.GetString(2));
                namespacesData.Add((nsId, nsPath, lastUpdated));
            }
            await reader.CloseAsync().ConfigureAwait(false);

            // Process each namespace
            foreach (var (nsId, nsPath, lastUpdated) in namespacesData)
            {
                var nsCache = new WmiNamespaceCache
                {
                    NamespacePath = nsPath,
                    LastUpdatedUtc = lastUpdated,
                    Classes = new List<WmiClassCache>()
                };

                // Load classes for this namespace
                await using var classCmd = conn.CreateCommand();
                classCmd.CommandText = "SELECT ClassId, ClassName, IsSystemClass, IsEventClass FROM Classes WHERE NamespaceId = @nsId AND IsExpired = 0";
                classCmd.Parameters.AddWithValue("@nsId", nsId);
                await using var classReader = await classCmd.ExecuteReaderAsync().ConfigureAwait(false);

                var classesData = new List<(int classId, string className, bool isSystem, bool isEvent)>();
                while (await classReader.ReadAsync().ConfigureAwait(false))
                {
                    var classId = classReader.GetInt32(0);
                    var className = classReader.GetString(1);
                    var isSystem = classReader.GetInt32(2) != 0;
                    var isEvent = classReader.GetInt32(3) != 0;
                    classesData.Add((classId, className, isSystem, isEvent));
                }
                await classReader.CloseAsync().ConfigureAwait(false);

                // Process each class
                foreach (var (classId, className, isSystem, isEvent) in classesData)
                {
                    var classCache = new WmiClassCache
                    {
                        ClassName = className,
                        IsSystemClass = isSystem,
                        IsEventClass = isEvent,
                        Properties = new List<WmiPropertyCache>()
                    };

                    // Load properties for this class
                    await using var propCmd = conn.CreateCommand();
                    propCmd.CommandText = "SELECT PropertyName, PropertyType FROM ClassProperties WHERE ClassId = @classId AND IsExpired = 0";
                    propCmd.Parameters.AddWithValue("@classId", classId);
                    await using var propReader = await propCmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await propReader.ReadAsync().ConfigureAwait(false))
                    {
                        classCache.Properties.Add(new WmiPropertyCache
                        {
                            Name = propReader.GetString(0),
                            Type = propReader.GetString(1)
                        });
                    }
                    nsCache.Classes.Add(classCache);
                }
                result[nsCache.NamespacePath] = nsCache;
            }

            lock (_lock) { _memoryCache = result; }
            return result.Values.ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading cache from {CacheFilePath}", CacheFilePath);
            lock (_lock) { _memoryCache = new Dictionary<string, WmiNamespaceCache>(StringComparer.OrdinalIgnoreCase); }
            return _memoryCache.Values.ToList();
        }
    }

    public async Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
    {
        try
        {
            await using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

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

            await tx.CommitAsync().ConfigureAwait(false);

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
            Log.Error(ex, "Error updating namespace cache for {NamespacePath}", namespaceCache.NamespacePath);
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
        LastUpdatedUtc TEXT,
        IsExpired INTEGER DEFAULT 0
    );
    CREATE TABLE IF NOT EXISTS Classes (
        ClassId INTEGER PRIMARY KEY AUTOINCREMENT,
        NamespaceId INTEGER,
        ClassName TEXT,
        IsSystemClass INTEGER,
        IsEventClass INTEGER,
        IsExpired INTEGER DEFAULT 0,
        FOREIGN KEY(NamespaceId) REFERENCES Namespaces(NamespaceId) ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS idx_Classes_NamespaceId ON Classes(NamespaceId);
    CREATE INDEX IF NOT EXISTS idx_Classes_IsExpired ON Classes(IsExpired);
    CREATE TABLE IF NOT EXISTS ClassProperties (
        PropertyId INTEGER PRIMARY KEY AUTOINCREMENT,
        ClassId INTEGER,
        PropertyName TEXT,
        PropertyType TEXT,
        IsExpired INTEGER DEFAULT 0,
        FOREIGN KEY(ClassId) REFERENCES Classes(ClassId) ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS idx_ClassProperties_ClassId ON ClassProperties(ClassId);
    CREATE INDEX IF NOT EXISTS idx_ClassProperties_IsExpired ON ClassProperties(IsExpired);
    CREATE INDEX IF NOT EXISTS idx_Namespaces_IsExpired ON Namespaces(IsExpired);
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
            conn = new SqliteConnection($"Data Source={CacheFilePath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
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
                Log.Debug("Schema version mismatch: expected {ExpectedVersion}, found {FoundVersion}. Recreating database.", CurrentSchemaVersion, dbVersion);
            }
            else
            {
                Log.Debug("Schema version is up-to-date: {Version}.", dbVersion);
            }
        }
        catch
        {
            recreate = true;
        }
        finally
        {
            conn?.Close();
            if (conn != null)
            {
                SqliteConnection.ClearPool(conn);
            }
            conn?.Dispose();
        }

        // Only delete the file when no connection is open
        if (recreate)
        {
            if (File.Exists(CacheFilePath))
            {
                // Proper disposal should have closed all connections, but file may still be locked
                // by another process or pending finalizers. Retry with delays.
                bool deleted = false;
                // Wait for a short time in case another process is using the file
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(CacheFilePath);
                        Log.Debug("Cache.db deleted successfully.");
                        deleted = true;
                        break;
                    }
                    catch (IOException ex)
                    {
                        Log.Warning(ex, "Cache.db could not be deleted, retry attempt [{Attempt}/5]...", i + 1);
                        if (i < 4) // Don't sleep on last attempt
                        {
                            // Use synchronous delay since this method is not async
                            // TODO: Consider making EnsureDatabase async to use Task.Delay
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Cache.db could not be deleted, retry attempt [{Attempt}/5]...", i + 1);
                        if (i < 4) // Don't sleep on last attempt
                        {
                            // Use synchronous delay since this method is not async
                            // TODO: Consider making EnsureDatabase async to use Task.Delay
                            System.Threading.Thread.Sleep(300);
                        }
                    }
                }

                if (!deleted)
                {
                    // If we couldn't delete after retries, log the warning but continue
                    // We'll overwrite the file when creating the new database
                    Log.Warning("Cache.db could not be deleted after multiple attempts. It may be in use by another process.");
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
    /// Asynchronously marks expired namespace cache entries and their related data as expired in the database,
    /// and permanently removes records that have been expired for longer than the prune interval.
    /// </summary>
    private async Task PruneExpiredCacheAsync()
    {
        try
        {
            await using var conn = new SqliteConnection($"Data Source={CacheFilePath}");
            await conn.OpenAsync().ConfigureAwait(false);
            await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

            // Mark expired namespaces (soft delete)
            await using var markNsCmd = conn.CreateCommand();
            markNsCmd.CommandText = @"
                UPDATE Namespaces
                SET IsExpired = 1
                WHERE datetime(LastUpdatedUtc) < datetime('now', @expiration)";
            markNsCmd.Parameters.AddWithValue("@expiration", $"-{Expiration.TotalDays} days");
            await markNsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Mark expired classes (soft delete)
            await using var markClassCmd = conn.CreateCommand();
            markClassCmd.CommandText = @"
                UPDATE Classes
                SET IsExpired = 1
                WHERE NamespaceId IN (
                    SELECT NamespaceId FROM Namespaces WHERE IsExpired = 1
                )";
            await markClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Mark expired properties (soft delete)
            await using var markPropCmd = conn.CreateCommand();
            markPropCmd.CommandText = @"
                UPDATE ClassProperties
                SET IsExpired = 1
                WHERE ClassId IN (
                    SELECT ClassId FROM Classes WHERE IsExpired = 1
                )";
            await markPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Permanently remove records that have been expired for longer than the prune interval
            await using var delPropCmd = conn.CreateCommand();
            delPropCmd.CommandText = @"
                DELETE FROM ClassProperties
                WHERE IsExpired = 1
                AND ClassId IN (
                    SELECT c.ClassId
                    FROM Classes c
                    INNER JOIN Namespaces n ON c.NamespaceId = n.NamespaceId
                    WHERE c.IsExpired = 1
                    AND datetime(n.LastUpdatedUtc) < datetime('now', @pruneInterval)
                )";
            delPropCmd.Parameters.AddWithValue("@pruneInterval", $"-{PruneInterval.TotalDays} days");
            await delPropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await using var delClassCmd = conn.CreateCommand();
            delClassCmd.CommandText = @"
                DELETE FROM Classes
                WHERE IsExpired = 1
                AND NamespaceId IN (
                    SELECT NamespaceId
                    FROM Namespaces
                    WHERE IsExpired = 1
                    AND datetime(LastUpdatedUtc) < datetime('now', @pruneInterval)
                )";
            delClassCmd.Parameters.AddWithValue("@pruneInterval", $"-{PruneInterval.TotalDays} days");
            await delClassCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await using var delNsCmd = conn.CreateCommand();
            delNsCmd.CommandText = @"
                DELETE FROM Namespaces
                WHERE IsExpired = 1
                AND datetime(LastUpdatedUtc) < datetime('now', @pruneInterval)";
            delNsCmd.Parameters.AddWithValue("@pruneInterval", $"-{PruneInterval.TotalDays} days");
            await delNsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await tx.CommitAsync().ConfigureAwait(false);

            // Update in-memory cache to remove expired entries
            lock (_lock)
            {
                if (_memoryCache != null)
                {
                    var expiredPaths = _memoryCache
                        .Where(kvp => kvp.Value.LastUpdatedUtc.Add(Expiration) < DateTime.UtcNow)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var path in expiredPaths)
                    {
                        _memoryCache.Remove(path);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error pruning expired cache");
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
                Log.Error(ex, "Error in background pruning of expired cache entries");
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
            await using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM Classes WHERE NamespaceId = @nsId";
            delCmd.Parameters.AddWithValue("@nsId", nsId);
            await delCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return;
        }

        await using var delClassCmd = conn.CreateCommand();
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
            await using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM ClassProperties WHERE ClassId = @classId";
            delCmd.Parameters.AddWithValue("@classId", classId);
            await delCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return;
        }

        await using var delPropCmd = conn.CreateCommand();
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
        // Check if class exists (including expired)
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT ClassId, IsExpired FROM Classes WHERE NamespaceId = @nsId AND ClassName = @cn";
        checkCmd.Parameters.AddWithValue("@nsId", nsId);
        checkCmd.Parameters.AddWithValue("@cn", classCache.ClassName);

        var result = await checkCmd.ExecuteScalarAsync().ConfigureAwait(false);

        if (result != null)
        {
            // Class exists - reactivate and update
            var classId = Convert.ToInt64(result);

            await using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Classes
                SET IsSystemClass = @sys, IsEventClass = @evt, IsExpired = 0
                WHERE ClassId = @classId";
            updateCmd.Parameters.AddWithValue("@sys", classCache.IsSystemClass ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@evt", classCache.IsEventClass ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@classId", classId);
            await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            return classId;
        }
        else
        {
            // Class doesn't exist - create new
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Classes (NamespaceId, ClassName, IsSystemClass, IsEventClass, IsExpired)
                VALUES (@nsId, @cn, @sys, @evt, 0)";
            insertCmd.Parameters.AddWithValue("@nsId", nsId);
            insertCmd.Parameters.AddWithValue("@cn", classCache.ClassName);
            insertCmd.Parameters.AddWithValue("@sys", classCache.IsSystemClass ? 1 : 0);
            insertCmd.Parameters.AddWithValue("@evt", classCache.IsEventClass ? 1 : 0);
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            insertCmd.CommandText = "SELECT ClassId FROM Classes WHERE NamespaceId = @nsId AND ClassName = @cn";
            return (long)(await insertCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
        }
    }

    /// <summary>
    /// Upserts a namespace and returns its ID.
    /// </summary>
    private static async Task<long> UpsertNamespaceAsync(SqliteConnection conn, WmiNamespaceCache namespaceCache)
    {
        // First, check if namespace exists (including expired ones)
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT NamespaceId, IsExpired FROM Namespaces WHERE NamespacePath = @ns";
        checkCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);

        var result = await checkCmd.ExecuteScalarAsync().ConfigureAwait(false);

        if (result != null)
        {
            // Namespace exists - reactivate and update
            var nsId = Convert.ToInt64(result);

            await using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Namespaces
                SET LastUpdatedUtc = @dt, IsExpired = 0
                WHERE NamespaceId = @nsId";
            updateCmd.Parameters.AddWithValue("@dt", namespaceCache.LastUpdatedUtc.ToString("o"));
            updateCmd.Parameters.AddWithValue("@nsId", nsId);
            await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            return nsId;
        }
        else
        {
            // Namespace doesn't exist - create new
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Namespaces (NamespacePath, LastUpdatedUtc, IsExpired)
                VALUES (@ns, @dt, 0)";
            insertCmd.Parameters.AddWithValue("@ns", namespaceCache.NamespacePath);
            insertCmd.Parameters.AddWithValue("@dt", namespaceCache.LastUpdatedUtc.ToString("o"));
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            insertCmd.CommandText = "SELECT NamespaceId FROM Namespaces WHERE NamespacePath = @ns";
            return (long)(await insertCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
        }
    }

    /// <summary>
    /// Upserts a property for a class.
    /// </summary>
    private static async Task UpsertPropertyAsync(SqliteConnection conn, long classId, WmiPropertyCache prop)
    {
        // Check if property exists (including expired)
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT PropertyId, IsExpired FROM ClassProperties WHERE ClassId = @classId AND PropertyName = @pn";
        checkCmd.Parameters.AddWithValue("@classId", classId);
        checkCmd.Parameters.AddWithValue("@pn", prop.Name);

        var result = await checkCmd.ExecuteScalarAsync().ConfigureAwait(false);

        if (result != null)
        {
            // Property exists - reactivate and update
            await using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE ClassProperties
                SET PropertyType = @pt, IsExpired = 0
                WHERE ClassId = @classId AND PropertyName = @pn";
            updateCmd.Parameters.AddWithValue("@pt", prop.Type);
            updateCmd.Parameters.AddWithValue("@classId", classId);
            updateCmd.Parameters.AddWithValue("@pn", prop.Name);
            await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        else
        {
            // Property doesn't exist - create new
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO ClassProperties (ClassId, PropertyName, PropertyType, IsExpired)
                VALUES (@classId, @pn, @pt, 0)";
            insertCmd.Parameters.AddWithValue("@classId", classId);
            insertCmd.Parameters.AddWithValue("@pn", prop.Name);
            insertCmd.Parameters.AddWithValue("@pt", prop.Type);
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}