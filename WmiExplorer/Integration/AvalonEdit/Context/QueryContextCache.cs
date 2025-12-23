namespace WmiExplorer.Integration.AvalonEdit.Context;

/// <summary>
/// Enhanced query context cache with LRU eviction and performance optimization.
/// </summary>
internal static class QueryContextCache
{
    private static readonly Dictionary<string, QueryContext> _contextCache = new();
    private static readonly Queue<string> _cacheKeys = new();
    private static readonly object _cacheLock = new();
    private const int MaxCacheSize = 100;

    /// <summary>
    /// Attempts to get a cached context for the given query text.
    /// </summary>
    public static bool TryGetContext(string queryText, out QueryContext? context)
    {
        lock (_cacheLock)
        {
            return _contextCache.TryGetValue(queryText, out context);
        }
    }

    /// <summary>
    /// Adds a context to the cache with LRU eviction.
    /// </summary>
    public static void AddContext(string queryText, QueryContext context)
    {
        lock (_cacheLock)
        {
            // If already exists, update it
            if (_contextCache.ContainsKey(queryText))
            {
                _contextCache[queryText] = context;
                return;
            }

            // Implement LRU cache eviction
            if (_contextCache.Count >= MaxCacheSize)
            {
                var oldestKey = _cacheKeys.Dequeue();
                _contextCache.Remove(oldestKey);
            }

            _contextCache[queryText] = context;
            _cacheKeys.Enqueue(queryText);
        }
    }

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _contextCache.Clear();
            _cacheKeys.Clear();
        }
    }

    /// <summary>
    /// Gets the current cache size.
    /// </summary>
    public static int CacheSize
    {
        get
        {
            lock (_cacheLock)
            {
                return _contextCache.Count;
            }
        }
    }
}
