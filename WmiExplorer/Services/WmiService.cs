using System.Management;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Cache;

namespace WmiExplorer.Services;

public class WmiService : IWmiService, IDisposable
{
    private readonly ICacheService _cacheService;
    private readonly List<IDisposable> _disposables = new();

    private readonly EnumerationOptions _enumOptions = new EnumerationOptions
    {
        UseAmendedQualifiers = true,
        EnumerateDeep = true
    };

    // Cache for provider CLSIDs to avoid repeated WMI queries
    private readonly Dictionary<string, string?> _providerClsidCache = new();

    public WmiService(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public WmiOperationMode OperationMode { get; set; } = WmiOperationMode.Asynchronous;

    /// <summary>
    /// Creates a connected ManagementScope for a namespace path and optional connection options
    /// </summary>
    public ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions? options = null)
    {
        var scope = options != null ? new ManagementScope(namespacePath, options) : new ManagementScope(namespacePath);
        EnsureScopeConnected(scope);
        return scope;
    }

    /// <summary>
    /// Executes a search for classes, methods, or properties based on the search type.    /// Returns tuples where first item is the search match (ManagementClass, MethodData, or PropertyData) and second is the parent class.
    /// </summary>
    public Task<IEnumerable<(object match, ManagementBaseObject parent)>> ExecuteSearchAsync(
        ManagementScope scope,
        WmiSearchType searchType,
        string searchText,
        bool recursive,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            EnsureScopeConnected(scope);
            var results = new List<(object match, ManagementBaseObject parent)>();

            // Search in the current namespace
            await SearchInNamespaceAsync(scope, searchType, searchText, results, cancellationToken);

            // If recursive search is enabled, search in child namespaces
            if (recursive && !cancellationToken.IsCancellationRequested)
            {
                await SearchInChildNamespacesRecursivelyAsync(scope, searchType, searchText, results, cancellationToken);
            }

            return (IEnumerable<(object, ManagementBaseObject)>)results;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously gets child namespaces for a given WMI namespace
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default)
    {
        EnsureScopeConnected(scope);
        return await Task.Run(() => GetChildNamespacesSync(scope, cancellationToken), cancellationToken);

        // if (OperationMode == WmiOperationMode.Synchronous)
        //     return await Task.Run(() => GetChildNamespacesSync(scope, cancellationToken), cancellationToken);
        // else
        //     return await GetChildNamespacesAsyncInternal(scope, cancellationToken);
    }

    /// <summary>
    /// Asynchronously gets classes for a given WMI namespace
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetClassesAsync(ManagementScope scope, WmiClassTypeFlags classTypeFilter = WmiClassTypeFlags.All, CancellationToken cancellationToken = default)
    {
        EnsureScopeConnected(scope);
        if (OperationMode == WmiOperationMode.Synchronous)
            return await Task.Run(() => GetClassesSync(scope, classTypeFilter, cancellationToken), cancellationToken);
        else
            return await GetClassesAsyncInternal(scope, classTypeFilter, cancellationToken);
    }

    /// <summary>
    /// Asynchronously gets instances for a given WMI class
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetInstancesAsync(ManagementScope scope, string className, CancellationToken cancellationToken = default)
    {
        EnsureScopeConnected(scope);
        if (OperationMode == WmiOperationMode.Synchronous)
            return await Task.Run(() => GetInstancesSync(scope, className, cancellationToken), cancellationToken);
        else
            return await GetInstancesAsyncInternal(scope, className, cancellationToken);
    }

    /// <summary>
    /// Gets the CLSID for a WMI provider by name (synchronous, returns null if not found or error)
    /// </summary>
    public string? GetProviderClsid(ManagementScope scope, string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        var cacheKey = providerName.Trim();
        // If cache is populated, only use cache
        if (_providerClsidCache.Count > 0)
        {
            return _providerClsidCache.TryGetValue(cacheKey, out var cachedClsid) ? cachedClsid : null;
        }

        // Populate cache with all provider CLSIDs on first run
        try
        {
            var server = scope.Path?.Server;
            var options = scope.Options;
            var rootDefaultPath = !string.IsNullOrWhiteSpace(server)
                ? $"\\\\{server}\\root\\default"
                : "\\.\\root\\default";
            var rootDefaultScope = CreateManagementScope(rootDefaultPath, options);
            var query = new ObjectQuery("SELECT Name, CLSID FROM __Win32Provider");
            using var searcher = new ManagementObjectSearcher(rootDefaultScope, query);
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                var clsid = obj["CLSID"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name) && !_providerClsidCache.ContainsKey(name))
                    _providerClsidCache[name] = clsid;
            }
            // After populating, return from cache
            return _providerClsidCache.TryGetValue(cacheKey, out var result) ? result : null;
        }
        catch
        {
            // Ignore errors, return null
        }
        return null;
    }

    /// <summary>
    /// Gets a root ManagementObject for a given namespace path
    /// </summary>
    public async Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default)
    {
        // Always use sync for root namespace, even in async mode
        return await Task.Run(() => GetRootNamespaceSync(namespacePath, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~WmiService()
    {
        Dispose(false);
    }

    /// <summary>
    /// Builds a WQL query string based on the class type filter
    /// </summary>
    private string BuildClassQueryFromFilter(WmiClassTypeFlags classTypeFilter)
    {
        // For All filter, use the simplest possible query
        if (classTypeFilter == WmiClassTypeFlags.All)
            return "SELECT * FROM meta_class";

        // Start with a simple base query without the problematic LIKE '%'
        string query = "SELECT * FROM meta_class WHERE __Class LIKE '%'";

        // Remove System class filtering here; always return system classes
        // Only filter CIM, MSFT, Perf
        if ((classTypeFilter & WmiClassTypeFlags.CIM) != WmiClassTypeFlags.CIM)
            query += " AND NOT __Class LIKE \"CIM[_]%\"";

        if ((classTypeFilter & WmiClassTypeFlags.MSFT) != WmiClassTypeFlags.MSFT)
            query += " AND NOT __Class LIKE \"MSFT[_]%\"";

        if ((classTypeFilter & WmiClassTypeFlags.Perf) != WmiClassTypeFlags.Perf)
            query += " AND NOT __Class LIKE \"Win32_Perf%\"";

        return query;
    }

    /// <summary>
    /// Caches lightweight class metadata for a namespace.
    /// </summary>
    private async Task CacheNamespaceClassMetadata(string namespacePath, IEnumerable<ManagementObject> classes)
    {
        var classCaches = new List<WmiClassCache>();
        foreach (var mo in classes)
        {
            try
            {
                var className = mo["__Class"]?.ToString() ?? string.Empty;
                var relativePath = mo.Path?.RelativePath ?? string.Empty;
                var isSystem = className.StartsWith("__");
                var derivation = mo["__Derivation"] as string[] ?? Array.Empty<string>();
                var isEvent = derivation.Contains("__Event") || className == "__Event";

                // Get property name/type pairs, excluding system properties (those starting with "__")
                var propertyList = new List<WmiPropertyCache>();
                try
                {
                    foreach (PropertyData prop in mo.Properties)
                    {
                        if (!prop.Name.StartsWith("__"))
                        {
                            propertyList.Add(new WmiPropertyCache
                            {
                                Name = prop.Name,
                                Type = prop.Type.ToString()
                            });
                        }
                    }
                }
                catch { /* Ignore property enumeration errors */ }

                classCaches.Add(new WmiClassCache
                {
                    ClassName = className,
                    RelativePath = relativePath,
                    IsSystemClass = isSystem,
                    IsEventClass = isEvent,
                    Properties = propertyList
                });
            }
            catch { /* Ignore individual class errors */ }
        }
        var nsCache = new WmiNamespaceCache
        {
            NamespacePath = namespacePath,
            LastUpdatedUtc = DateTime.UtcNow,
            Classes = classCaches
        };
        await _cacheService.UpdateNamespaceCacheAsync(nsCache);
    }

    /// <summary>
    /// Optimized: Ensures scope is connected only if not already connected
    /// </summary>
    private void EnsureScopeConnected(ManagementScope scope)
    {
        if (!scope.IsConnected)
        {
            try
            {
                scope.Connect();
            }
            catch (ManagementException mex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI ManagementException connecting scope: {mex.Message}");
                throw;
            }
            catch (UnauthorizedAccessException uex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI UnauthorizedAccessException connecting scope: {uex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI Exception connecting scope: {ex.Message}");
                throw;
            }
        }
    }

    private async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsyncInternal(ManagementScope scope, CancellationToken cancellationToken)
    {
        var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
        _disposables.Add(nsClass);
        return await RunWmiListAsync(obs => nsClass.GetInstances(obs), cancellationToken);
    }

    private IEnumerable<ManagementObject> GetChildNamespacesSync(ManagementScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
        _disposables.Add(nsClass);
        var instances = nsClass.GetInstances();
        _disposables.Add(instances);
        foreach (ManagementObject m in instances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _disposables.Add(m);
            result.Add(m);
        }
        return result;
    }

    private async Task<IEnumerable<ManagementObject>> GetClassesAsyncInternal(ManagementScope scope, WmiClassTypeFlags classTypeFilter, CancellationToken cancellationToken)
    {
        string queryString = BuildClassQueryFromFilter(classTypeFilter);
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        var result = await RunWmiListAsync(obs => searcher.Get(obs), cancellationToken);
        // Fire-and-forget cache update
        try { _ = Task.Run(() => CacheNamespaceClassMetadata(scope.Path?.Path ?? string.Empty, result)); } catch { /* Ignore cache errors */ }
        return result;
    }

    private IEnumerable<ManagementObject> GetClassesSync(ManagementScope scope, WmiClassTypeFlags classTypeFilter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        string queryString = BuildClassQueryFromFilter(classTypeFilter);
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        foreach (ManagementObject classObject in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();
            _disposables.Add(classObject);
            result.Add(classObject);
        }
        // Fire-and-forget cache update
        try { _ = Task.Run(() => CacheNamespaceClassMetadata(scope.Path?.Path ?? string.Empty, result)); } catch { /* Ignore cache errors */ }
        return result;
    }

    private async Task<IEnumerable<ManagementObject>> GetInstancesAsyncInternal(ManagementScope scope, string className, CancellationToken cancellationToken)
    {
        var query = new ObjectQuery($"SELECT * FROM {className}");
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        return await RunWmiListAsync(obs => searcher.Get(obs), cancellationToken);
    }

    private IEnumerable<ManagementObject> GetInstancesSync(ManagementScope scope, string className, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        var query = new ObjectQuery($"SELECT * FROM {className}");
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        foreach (ManagementObject instanceObject in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();
            _disposables.Add(instanceObject);
            result.Add(instanceObject);
        }
        return result;
    }

    private ManagementObject? GetRootNamespaceSync(string namespacePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var mPath = new ManagementPath(namespacePath);
            var mScope = new ManagementScope(mPath);
            EnsureScopeConnected(mScope);
            var oOptions = new ObjectGetOptions();
            var mObject = new ManagementObject(mScope, mPath, oOptions);
            return mObject;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating root namespace: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Helper for async WMI queries returning a list
    /// </summary>
    private async Task<List<ManagementObject>> RunWmiListAsync(Action<ManagementOperationObserver> startAction, CancellationToken cancellationToken)
    {
        var result = new List<ManagementObject>();
        var tcs = new TaskCompletionSource<List<ManagementObject>>();
        var observer = new ManagementOperationObserver();
        var cancelCalled = false;

        observer.ObjectReady += (sender, e) =>
        {
            if (e.NewObject is ManagementObject obj)
            {
                _disposables.Add(obj);
                result.Add(obj);
            }
        };

        observer.Completed += (sender, e) =>
        {
            if (e.Status == ManagementStatus.NoError)
            {
                tcs.TrySetResult(result);
            }
            else if (e.Status == ManagementStatus.CallCanceled || e.Status == ManagementStatus.OperationCanceled)
            {
                // Return partial results when operation was canceled - don't treat as error
                tcs.TrySetResult(result);
            }
            else
            {
                tcs.TrySetException(new ManagementException($"WMI async query failed: {e.Status}"));
            }
        };

        observer.Progress += (sender, e) =>
        {
            if (cancellationToken.IsCancellationRequested && !cancelCalled)
            {
                cancelCalled = true;
                // Make observer.Cancel() non-blocking by running it on a background thread
                Task.Run(() =>
                {
                    try
                    {
                        observer.Cancel();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error canceling WMI observer: {ex.Message}");
                    }
                });
                // Don't set as cancelled here - let the Completed event handle it and return partial results
            }
        };

        using (cancellationToken.Register(() =>
        {
            if (!cancelCalled)
            {
                cancelCalled = true;
                // Make observer.Cancel() non-blocking by running it on a background thread
                Task.Run(() =>
                {
                    try
                    {
                        observer.Cancel();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error canceling WMI observer: {ex.Message}");
                    }
                });
                // Don't set as cancelled here - let the Completed event handle it and return partial results
            }
        }))
        {
            try
            {
                startAction(observer);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Recursively searches in child namespaces
    /// </summary>
    private async Task SearchInChildNamespacesRecursivelyAsync(
        ManagementScope parentScope,
        WmiSearchType searchType,
        string searchText,
        List<(object match, ManagementBaseObject parent)> results,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get child namespaces for the current scope
            var childNamespaces = await GetChildNamespacesAsync(parentScope, cancellationToken);

            foreach (var childNamespace in childNamespaces)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Get the namespace name from the child namespace object
                    var namespaceName = childNamespace.Properties["Name"]?.Value?.ToString();
                    if (string.IsNullOrEmpty(namespaceName)) continue;

                    // Build the child namespace path
                    var parentPath = parentScope.Path?.Path ?? string.Empty;
                    var childNamespacePath = $"{parentPath}\\{namespaceName}";

                    // Create a new scope for the child namespace
                    var childScope = CreateManagementScope(childNamespacePath, parentScope.Options);

                    // Search in the child namespace
                    await SearchInNamespaceAsync(childScope, searchType, searchText, results, cancellationToken);

                    // Recursively search in grandchild namespaces
                    await SearchInChildNamespacesRecursivelyAsync(childScope, searchType, searchText, results, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other namespaces
                    System.Diagnostics.Debug.WriteLine($"Error searching in child namespace: {ex.Message}");
                    // Continue processing other child namespaces
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't propagate to avoid breaking the entire search
            System.Diagnostics.Debug.WriteLine($"Error getting child namespaces for recursive search: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for classes, methods, or properties in a specific namespace
    /// </summary>
    private async Task SearchInNamespaceAsync(
        ManagementScope scope,
        WmiSearchType searchType,
        string searchText,
        List<(object match, ManagementBaseObject parent)> results,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            switch (searchType)
            {
                case WmiSearchType.Class:
                    var classQuery = new SelectQuery("meta_class");
                    using (var searcher = new ManagementObjectSearcher(scope, classQuery, _enumOptions))
                    {
                        foreach (ManagementClass wmiClass in searcher.Get())
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            var className = wmiClass["__Class"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(searchText) || className.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            {
                                // For classes, match and parent are the same
                                results.Add((wmiClass, wmiClass));
                            }
                        }
                    }
                    break;

                case WmiSearchType.Method:
                    var methodClassQuery = new SelectQuery("meta_class");
                    using (var searcher = new ManagementObjectSearcher(scope, methodClassQuery, _enumOptions))
                    {
                        foreach (ManagementClass wmiClass in searcher.Get())
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            foreach (MethodData method in wmiClass.Methods)
                            {
                                if (string.IsNullOrWhiteSpace(searchText) || method.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Return the MethodData object directly
                                    results.Add((method, wmiClass));
                                }
                            }
                        }
                    }
                    break;

                case WmiSearchType.Property:
                    var propertyClassQuery = new SelectQuery("meta_class");
                    using (var searcher = new ManagementObjectSearcher(scope, propertyClassQuery, _enumOptions))
                    {
                        foreach (ManagementClass wmiClass in searcher.Get())
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            foreach (PropertyData prop in wmiClass.Properties)
                            {
                                if (string.IsNullOrWhiteSpace(searchText) || prop.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Return the PropertyData object directly
                                    results.Add((prop, wmiClass));
                                }
                            }
                        }
                    }
                    break;
            }
        }, cancellationToken);
    }

    #region IDisposable
    private bool _disposed;

    /// <summary>
    /// Disposes of the managed and unmanaged resources
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }
                _disposables.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Implements IDisposable to clean up WMI resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}