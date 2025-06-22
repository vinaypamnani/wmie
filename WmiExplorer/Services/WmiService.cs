using System.Management;
using System.Windows.Threading;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
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
        Log.Information("WmiService initialized with operation mode: {OperationMode}", OperationMode);
    }

    public WmiOperationMode OperationMode { get; set; } = WmiOperationMode.Asynchronous;

    /// <summary>
    /// Creates a connected ManagementScope for a namespace path and optional connection options
    /// </summary>
    public ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions connectionOptions)
    {
        if (string.IsNullOrWhiteSpace(namespacePath))
            throw new ArgumentException("Namespace path cannot be null or empty", nameof(namespacePath));
        if (connectionOptions == null)
            throw new ArgumentNullException(nameof(connectionOptions));

        var scope = new ManagementScope(namespacePath, connectionOptions);
        EnsureScopeConnected(scope);
        return scope;
    }

    /// <summary>
    /// Executes a WMI method asynchronously on an instance
    /// </summary>
    public async Task<ManagementBaseObject?> ExecuteMethodAsync(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default)
    {
        if (OperationMode == WmiOperationMode.Synchronous)
            return await ExecuteSyncWithTimeout(() => ExecuteMethodSync(instance, methodName, inputParameters, cancellationToken), cancellationToken);
        else
            return await ExecuteMethodAsyncInternal(instance, methodName, inputParameters, cancellationToken);
    }

    /// <summary>
    /// Executes a search for classes, methods, or properties based on the search type.
    /// Returns tuples where first item is the search match (ManagementClass, MethodData, or PropertyData) and second is the parent class.
    /// </summary>
    public Task<IEnumerable<(object match, ManagementBaseObject parent)>> ExecuteSearchAsync(
        ManagementScope scope,
        WmiSearchType searchType,
        string searchText,
        bool recursive,
        CancellationToken cancellationToken = default,
        Action<string>? progressCallback = null)
    {
        return Task.Run(async () =>
        {
            EnsureScopeConnected(scope);
            var results = new List<(object match, ManagementBaseObject parent)>();

            // Search in the current namespace
            var currentNamespace = scope.Path?.Path ?? "root";
            var friendlyNamespace = FormatNamespaceForDisplay(currentNamespace);
            progressCallback?.Invoke($"Searching namespace: {friendlyNamespace}");
            await SearchInNamespaceAsync(scope, searchType, searchText, results, cancellationToken);

            // If recursive search is enabled, search in child namespaces
            if (recursive && !cancellationToken.IsCancellationRequested)
            {
                await SearchInChildNamespacesRecursivelyAsync(scope, searchType, searchText, results, cancellationToken, progressCallback);
            }

            return (IEnumerable<(object, ManagementBaseObject)>)results;
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a static WMI method asynchronously on a class
    /// </summary>
    public async Task<ManagementBaseObject?> ExecuteStaticMethodAsync(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default)
    {
        if (OperationMode == WmiOperationMode.Synchronous)
            return await ExecuteSyncWithTimeout(() => ExecuteStaticMethodSync(managementClass, methodName, inputParameters, cancellationToken), cancellationToken);
        else
            return await ExecuteStaticMethodAsyncInternal(managementClass, methodName, inputParameters, cancellationToken);
    }

    /// <summary>
    /// Executes a WMI query asynchronously, using the specified query string.
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> ExecuteWmiQueryAsync(
        ManagementScope scope,
        string queryString,
        bool directRead, bool useAmendedQualifiers,
        CancellationToken cancellationToken = default)
    {
        Log.Debug("Executing WMI query: {Query} on scope: {Scope}", queryString, scope.Path?.Path ?? "Unknown");

        try
        {
            EnsureScopeConnected(scope);
            var userProvidedEnumOptions = new EnumerationOptions
            {
                DirectRead = directRead,
                UseAmendedQualifiers = useAmendedQualifiers

            };

            IEnumerable<ManagementObject> results;
            if (OperationMode == WmiOperationMode.Synchronous)
            {
                results = await ExecuteSyncWithTimeout(() => ExecuteWmiQuerySync(scope, queryString, userProvidedEnumOptions, cancellationToken), cancellationToken, 60000);
            }
            else
            {
                results = await ExecuteWmiQueryInternal(scope, queryString, userProvidedEnumOptions, cancellationToken);
            }

            var resultCount = results.Count();
            Log.Information("WMI query completed successfully. Returned {ResultCount} objects", resultCount);
            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute WMI query: {Query} on scope: {Scope}", queryString, scope.Path?.Path ?? "Unknown");
            throw;
        }
    }

    /// <summary>
    /// Asynchronously gets child namespaces for a given WMI namespace
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default)
    {
        var namespacePath = scope.Path?.Path ?? "Unknown";
        Log.Debug("Getting child namespaces for: {NamespacePath}", namespacePath);

        try
        {
            EnsureScopeConnected(scope);

            // Always use sync for child namespaces, even in async mode
            // return await ExecuteSyncWithTimeout(() => GetChildNamespacesSync(scope, cancellationToken), cancellationToken);

            IEnumerable<ManagementObject> results;
            if (OperationMode == WmiOperationMode.Synchronous)
                results = await ExecuteSyncWithTimeout(() => GetChildNamespacesSync(scope, cancellationToken), cancellationToken);
            else
                results = await GetChildNamespacesAsyncInternal(scope, cancellationToken);

            var childCount = results.Count();
            Log.Debug("Found {ChildCount} child namespaces for: {NamespacePath}", childCount, namespacePath);

            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting child namespaces for: {NamespacePath}", namespacePath);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously gets classes for a given WMI namespace
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetClassesAsync(ManagementScope scope, WmiClassEnumerationFlags classTypeFilter = WmiClassEnumerationFlags.All, CancellationToken cancellationToken = default)
    {
        EnsureScopeConnected(scope);
        if (OperationMode == WmiOperationMode.Synchronous)
            return await ExecuteSyncWithTimeout(() => GetClassesSync(scope, classTypeFilter, cancellationToken), cancellationToken);
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
            // Instance loading can take longer, especially for classes with many instances like Win32_Directory
            return await ExecuteSyncWithTimeout(() => GetInstancesSync(scope, className, cancellationToken), cancellationToken, 60000);
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
            Log.Debug("Error retrieving WMI provider CLSID for: {ProviderName}", providerName);
        }
        return null;
    }

    /// <summary>
    /// Gets a root ManagementObject for a given namespace path
    /// </summary>
    public async Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, ConnectionOptions connectionOptions, CancellationToken cancellationToken = default)
    {
        Log.Information("Connecting to WMI namespace: {NamespacePath}", namespacePath);

        try
        {
            // Always use sync for root namespace, even in async mode
            var result = await Task.Run(() => GetRootNamespaceSync(namespacePath, connectionOptions, cancellationToken), cancellationToken);

            if (result != null)
            {
                Log.Information("Successfully connected to WMI namespace: {NamespacePath}", namespacePath);
            }
            else
            {
                Log.Warning("Failed to connect to WMI namespace: {NamespacePath} - returned null", namespacePath);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error connecting to WMI namespace: {NamespacePath}", namespacePath);
            throw;
        }
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
    private string BuildClassQueryFromFilter(WmiClassEnumerationFlags classTypeFilter)
    {
        // For All filter, use the simplest possible query
        if (classTypeFilter == WmiClassEnumerationFlags.All)
            return "SELECT * FROM meta_class";

        // Start with a simple base query without the problematic LIKE '%'
        string query = "SELECT * FROM meta_class WHERE __Class LIKE '%'";

        // Remove System class filtering here; always return system classes
        // Only filter CIM, MSFT, Perf
        if ((classTypeFilter & WmiClassEnumerationFlags.CIM) != WmiClassEnumerationFlags.CIM)
            query += " AND NOT __Class LIKE \"CIM[_]%\"";

        if ((classTypeFilter & WmiClassEnumerationFlags.MSFT) != WmiClassEnumerationFlags.MSFT)
            query += " AND NOT __Class LIKE \"MSFT[_]%\"";

        if ((classTypeFilter & WmiClassEnumerationFlags.Perf) != WmiClassEnumerationFlags.Perf)
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
                var className = mo.Path?.ClassName ?? string.Empty;
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
    /// Uses a cooperative non-blocking approach to prevent UI freezes
    /// </summary>
    private void EnsureScopeConnected(ManagementScope scope)
    {
        if (!scope.IsConnected)
        {
            try
            {
                // Use a SynchronizationContext-aware technique to prevent UI thread blocking
                // while still maintaining the synchronous method signature
                var connectTask = Task.Run(() => scope.Connect());

                // Use a dispatcher frame approach to keep the UI responsive while waiting
                var frame = new DispatcherFrame();

                connectTask.ContinueWith(t =>
                {
                    // Stop the dispatcher frame when the task completes
                    frame.Continue = false;
                }, TaskScheduler.Default);

                // This will process UI messages until the frame is stopped
                Dispatcher.PushFrame(frame);

                // Now get the result (should be immediate as task is complete)
                // Will propagate exceptions if they occurred
                connectTask.GetAwaiter().GetResult();
            }
            catch (ManagementException mex)
            {
                Log.Error(mex, "WMI ManagementException while connecting to scope: {ScopePath}", scope?.Path?.Path ?? "Unknown");
                throw;
            }
            catch (UnauthorizedAccessException uex)
            {
                Log.Error(uex, "WMI UnauthorizedAccessException while connecting to scope: {ScopePath}", scope?.Path?.Path ?? "Unknown");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WMI Exception while connecting to scope: {ScopePath}", scope?.Path?.Path ?? "Unknown");
                throw;
            }
        }
    }

    /// <summary>
    /// Executes a WMI method asynchronously on an instance using ManagementOperationObserver
    /// </summary>
    private async Task<ManagementBaseObject?> ExecuteMethodAsyncInternal(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ManagementBaseObject?>();
        var observer = new ManagementOperationObserver();
        var cancelCalled = false;

        observer.ObjectReady += (sender, e) =>
        {
            if (e.NewObject is ManagementBaseObject result)
            {
                _disposables.Add(result);
                tcs.TrySetResult(result);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        };

        observer.Completed += (sender, e) =>
        {
            if (e.Status == ManagementStatus.NoError)
            {
                // Result already set in ObjectReady if applicable
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            }
            else if (e.Status == ManagementStatus.CallCanceled || e.Status == ManagementStatus.OperationCanceled)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            else
            {
                tcs.TrySetException(new ManagementException($"WMI async method execution failed: {e.Status}"));
            }
        };

        using (cancellationToken.Register(() =>
        {
            if (!cancelCalled)
            {
                cancelCalled = true;
                Task.Run(() =>
                {
                    try
                    {
                        observer.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error canceling WMI method observer");
                    }
                });
            }
        }))
        {
            try
            {
                instance.InvokeMethod(observer, methodName, inputParameters, null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a WMI method synchronously on an instance
    /// </summary>
    private ManagementBaseObject? ExecuteMethodSync(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return instance.InvokeMethod(methodName, inputParameters, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing WMI instance method '{MethodName}' on instance", methodName);
            throw;
        }
    }

    /// <summary>
    /// Executes a static WMI method asynchronously on a class using ManagementOperationObserver
    /// </summary>
    private async Task<ManagementBaseObject?> ExecuteStaticMethodAsyncInternal(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ManagementBaseObject?>();
        var observer = new ManagementOperationObserver();
        var cancelCalled = false;

        observer.ObjectReady += (sender, e) =>
        {
            if (e.NewObject is ManagementBaseObject result)
            {
                _disposables.Add(result);
                tcs.TrySetResult(result);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        };

        observer.Completed += (sender, e) =>
        {
            if (e.Status == ManagementStatus.NoError)
            {
                // Result already set in ObjectReady if applicable
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            }
            else if (e.Status == ManagementStatus.CallCanceled || e.Status == ManagementStatus.OperationCanceled)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            else
            {
                tcs.TrySetException(new ManagementException($"WMI async static method execution failed: {e.Status}"));
            }
        };

        using (cancellationToken.Register(() =>
        {
            if (!cancelCalled)
            {
                cancelCalled = true;
                Task.Run(() =>
                {
                    try
                    {
                        observer.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error canceling WMI static method observer");
                    }
                });
            }
        }))
        {
            try
            {
                managementClass.InvokeMethod(observer, methodName, inputParameters, null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a static WMI method synchronously on a class
    /// </summary>
    private ManagementBaseObject? ExecuteStaticMethodSync(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return managementClass.InvokeMethod(methodName, inputParameters, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing WMI static method '{MethodName}' on class", methodName);
            throw;
        }
    }

    /// <summary>
    /// Executes a synchronous WMI operation with timeout-based cancellation support
    /// </summary>
    private async Task<T> ExecuteSyncWithTimeout<T>(Func<T> operation, CancellationToken cancellationToken, int timeoutMs = 30000)
    {
        return await Task.Run(() =>
        {
            // Check cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Create a task to run the blocking operation
            var operationTask = Task.Run(operation, cancellationToken);

            // Wait for either completion or cancellation
            try
            {
                if (operationTask.Wait(timeoutMs, cancellationToken))
                {
                    return operationTask.Result;
                }
                else
                {
                    // Operation timed out - this gives us a way to break out of long-running synchronous WMI calls
                    throw new OperationCanceledException($"WMI synchronous operation timed out after {timeoutMs}ms - this may indicate a very large result set or unresponsive WMI provider");
                }
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                throw ex.InnerException;
            }
            catch (AggregateException ex)
            {
                // Unwrap other exceptions
                throw ex.InnerException ?? ex;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a WMI query asynchronously and returns the results.
    /// </summary>
    private async Task<IEnumerable<ManagementObject>> ExecuteWmiQueryInternal(
        ManagementScope scope,
        string queryString,
        EnumerationOptions enumOptions,
        CancellationToken cancellationToken)
    {
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, enumOptions);
        _disposables.Add(searcher);
        return await PerformWmiOperationAsync(obs => searcher.Get(obs), cancellationToken);
    }

    /// <summary>
    /// Executes a WMI query synchronously and returns the results.
    /// </summary>
    private IEnumerable<ManagementObject> ExecuteWmiQuerySync(
        ManagementScope scope,
        string queryString,
        EnumerationOptions enumOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, enumOptions);
        _disposables.Add(searcher);

        ManagementObjectCollection? collection = null;
        try
        {
            collection = searcher.Get();
            _disposables.Add(collection);

            foreach (ManagementObject obj in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _disposables.Add(obj);
                result.Add(obj);
            }
        }
        finally
        {
            collection?.Dispose();
        }
        return result;
    }

    /// <summary>
    /// Formats a namespace path for user-friendly display
    /// </summary>
    /// <param name="namespacePath">The full namespace path (e.g., \\.\root\cimv2)</param>
    /// <returns>A user-friendly namespace name (e.g., root\cimv2)</returns>
    private static string FormatNamespaceForDisplay(string namespacePath)
    {
        if (string.IsNullOrWhiteSpace(namespacePath))
            return "root";

        // Remove computer part (\\computer\ or \\.\ for local)
        if (namespacePath.StartsWith(@"\\"))
        {
            var segments = namespacePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                // Return everything after the computer name
                return string.Join("\\", segments.Skip(1));
            }
        }

        return namespacePath;
    }

    private async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsyncInternal(ManagementScope scope, CancellationToken cancellationToken)
    {
        var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
        _disposables.Add(nsClass);
        return await PerformWmiOperationAsync(obs => nsClass.GetInstances(obs), cancellationToken);
    }

    private IEnumerable<ManagementObject> GetChildNamespacesSync(ManagementScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
        _disposables.Add(nsClass);

        // Get the collection with periodic cancellation checks
        ManagementObjectCollection? instances = null;
        try
        {
            instances = nsClass.GetInstances();
            _disposables.Add(instances);

            foreach (ManagementObject m in instances)
            {
                // Check for cancellation between each namespace
                cancellationToken.ThrowIfCancellationRequested();
                _disposables.Add(m);
                result.Add(m);
            }
        }
        finally
        {
            instances?.Dispose();
        }

        return result;
    }

    private async Task<IEnumerable<ManagementObject>> GetClassesAsyncInternal(ManagementScope scope, WmiClassEnumerationFlags classTypeFilter, CancellationToken cancellationToken)
    {
        string queryString = BuildClassQueryFromFilter(classTypeFilter);
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        var result = await PerformWmiOperationAsync(obs => searcher.Get(obs), cancellationToken);
        // Fire-and-forget cache update
        try { _ = Task.Run(() => CacheNamespaceClassMetadata(scope.Path?.Path ?? string.Empty, result)); }
        catch { Log.Warning("Error updating class metadata cache for namespace: {NamespacePath}", scope.Path?.Path ?? "Unknown"); }
        return result;
    }

    private IEnumerable<ManagementObject> GetClassesSync(ManagementScope scope, WmiClassEnumerationFlags classTypeFilter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        string queryString = BuildClassQueryFromFilter(classTypeFilter);
        var query = new ObjectQuery(queryString);
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);

        // Get the collection with periodic cancellation checks
        ManagementObjectCollection? collection = null;
        try
        {
            collection = searcher.Get();
            _disposables.Add(collection);

            foreach (ManagementObject classObject in collection)
            {
                // Check for cancellation between each class
                cancellationToken.ThrowIfCancellationRequested();
                _disposables.Add(classObject);
                result.Add(classObject);
            }
        }
        finally
        {
            collection?.Dispose();
        }

        // Fire-and-forget cache update
        try { _ = Task.Run(() => CacheNamespaceClassMetadata(scope.Path?.Path ?? string.Empty, result)); }
        catch { Log.Warning("Error updating class metadata cache for namespace: {NamespacePath}", scope.Path?.Path ?? "Unknown"); }
        return result;
    }

    private async Task<IEnumerable<ManagementObject>> GetInstancesAsyncInternal(ManagementScope scope, string className, CancellationToken cancellationToken)
    {
        var query = new ObjectQuery($"SELECT * FROM {className}");
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);
        return await PerformWmiOperationAsync(obs => searcher.Get(obs), cancellationToken);
    }

    private IEnumerable<ManagementObject> GetInstancesSync(ManagementScope scope, string className, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ManagementObject>();
        var query = new ObjectQuery($"SELECT * FROM {className}");
        var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
        _disposables.Add(searcher);

        // Get the collection with periodic cancellation checks
        ManagementObjectCollection? collection = null;
        try
        {
            collection = searcher.Get();
            _disposables.Add(collection);

            foreach (ManagementObject instanceObject in collection)
            {
                // Check for cancellation between each instance
                cancellationToken.ThrowIfCancellationRequested();
                _disposables.Add(instanceObject);
                result.Add(instanceObject);
            }
        }
        finally
        {
            collection?.Dispose();
        }

        return result;
    }

    private ManagementObject? GetRootNamespaceSync(string namespacePath, ConnectionOptions connectionOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var mScope = CreateManagementScope(namespacePath, connectionOptions);
            var mPath = new ManagementPath(namespacePath);
            EnsureScopeConnected(mScope);
            var oOptions = new ObjectGetOptions();
            var mObject = new ManagementObject(mScope, mPath, oOptions);
            return mObject;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting root namespace: {NamespacePath}", namespacePath);
            throw;
        }
    }

    /// <summary>
    /// Helper for async WMI queries returning a list
    /// </summary>
    private async Task<List<ManagementObject>> PerformWmiOperationAsync(Action<ManagementOperationObserver> startAction, CancellationToken cancellationToken)
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
                        Log.Warning(ex, "Error canceling WMI observer during initial cancellation check");
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
                        Log.Warning(ex, "Error canceling WMI observer during cancellation token registration");
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
        CancellationToken cancellationToken,
        Action<string>? progressCallback = null)
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
                    var childNamespacePath = $"{parentPath}\\{namespaceName}";                    // Create a new scope for the child namespace
                    var childScope = CreateManagementScope(childNamespacePath, parentScope.Options);

                    // Report progress for the child namespace being searched
                    var friendlyChildNamespace = FormatNamespaceForDisplay(childNamespacePath);
                    progressCallback?.Invoke($"Searching namespace: {friendlyChildNamespace}");

                    // Search in the child namespace
                    await SearchInNamespaceAsync(childScope, searchType, searchText, results, cancellationToken);

                    // Recursively search in grandchild namespaces
                    await SearchInChildNamespacesRecursivelyAsync(childScope, searchType, searchText, results, cancellationToken, progressCallback);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other namespaces
                    Log.Warning(ex, "Error searching in child namespace during recursive search");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't propagate to avoid breaking the entire search
            Log.Warning(ex, "Error getting child namespaces for recursive search");
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