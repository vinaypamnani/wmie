namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// Simple cache for query context analysis to improve performance.
/// </summary>
internal class QueryContextCache
{
    private const int CacheSize = 10;

    private readonly Dictionary<string, (DateTime Timestamp, QueryContext Context)> _cache = new();
    private readonly object _lock = new();

    public void AddContext(string text, QueryContext context)
    {
        lock (_lock)
        {
            // Add or update the cache entry
            _cache[text] = (DateTime.UtcNow, context);

            // If the cache exceeds the size limit, remove the oldest entry
            if (_cache.Count > CacheSize)
            {
                var oldest = _cache.OrderBy(x => x.Value.Timestamp).First().Key;
                _cache.Remove(oldest);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    public bool TryGetContext(string text, out QueryContext? context)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(text, out var cached))
            {
                // Update the timestamp to mark it as recently used
                _cache[text] = (DateTime.UtcNow, cached.Context);
                context = cached.Context;
                return true;
            }

            context = null;
            return false;
        }
    }
}