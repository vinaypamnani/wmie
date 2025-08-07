using System.Management;
using System.Windows;
using System.Windows.Threading;
using WmiExplorer.Common.Cache;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;

namespace WmiExplorer.Services;

public class WmiService : IWmiService, IDisposable
{
    private readonly ICacheService _cacheService;
    private readonly List<IDisposable> _disposables = new();

    // Cache for provider instances per namespace
    private readonly Dictionary<string, Dictionary<string, ManagementObject>> _providerCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _providerCacheLock = new();
    private readonly ISettingsService _settingsService;

    public WmiService(ICacheService cacheService, ISettingsService settingsService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        OperationMode = _settingsService.OperationMode;
        Log.Information("WmiService initialized with operation mode: {OperationMode}", OperationMode);
    }

    public WmiOperationMode OperationMode { get; set; } = WmiOperationMode.Asynchronous;

    /// <summary>
    /// Gets provider instances from the __provider class in a namespace, using cache if available
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> CacheProviderInstancesAsync(ManagementScope scope, CancellationToken cancellationToken = default)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        var namespacePath = scope.Path?.Path ?? string.Empty; // Use full path for consistent caching
        if (string.IsNullOrEmpty(namespacePath))
            throw new ArgumentException("Scope must have a valid namespace path", nameof(scope));

        // Check cache first
        lock (_providerCacheLock)
        {
            if (_providerCache.TryGetValue(namespacePath, out var cachedProviders))
            {
                Log.Debug("Returning {ProviderCount} cached providers for namespace: {NamespacePath}", cachedProviders.Count, namespacePath);
                return cachedProviders.Values.ToList();
            }
        }

        try
        {
            var providerInstances = await ExecuteWmiQueryAsync(
                scope,
                "SELECT * FROM __provider",
                directRead: false,
                useAmendedQualifiers: true,
                enableLogging: false,
                cancellationToken);

            // Cache the providers
            var providerDict = new Dictionary<string, ManagementObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var providerInstance in providerInstances)
            {
                var providerName = providerInstance["Name"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(providerName))
                {
                    providerDict[providerName] = providerInstance;
                }
            }

            lock (_providerCacheLock)
            {
                _providerCache[namespacePath] = providerDict;
            }

            Log.Debug("Cached {ProviderCount} providers for namespace: {NamespacePath}", providerDict.Count, namespacePath);
            return providerInstances;
        }
        catch (Exception ex)
        {
            Log.Debug("Error retrieving provider instances from namespace: {NamespacePath} - {ErrorMessage}", namespacePath, ex.Message ?? "Unknown error");
            return Enumerable.Empty<ManagementObject>();
        }
    }

    /// <summary>
    /// Clears all provider caches
    /// </summary>
    public void ClearAllProviderCaches()
    {
        lock (_providerCacheLock)
        {
            foreach (var providers in _providerCache.Values)
            {
                foreach (var provider in providers.Values)
                {
                    provider?.Dispose();
                }
            }
            _providerCache.Clear();
            Log.Debug("Cleared all provider caches");
        }
    }

    /// <summary>
    /// Clears the provider cache for a specific namespace
    /// </summary>
    /// <param name="namespacePath">The namespace path to clear cache for</param>
    public void ClearProviderCache(string namespacePath)
    {
        if (string.IsNullOrEmpty(namespacePath))
            return;

        lock (_providerCacheLock)
        {
            if (_providerCache.TryGetValue(namespacePath, out var providers))
            {
                foreach (var provider in providers.Values)
                {
                    provider?.Dispose();
                }
                _providerCache.Remove(namespacePath);
                Log.Debug("Cleared provider cache for namespace: {NamespacePath}", namespacePath);
            }
        }
    }

    /// <summary>
    /// Creates a ManagementScope for a given namespace path and optional connection options
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
    /// Deletes a WMI instance asynchronously.
    /// </summary>
    public async Task DeleteInstanceAsync(ManagementObject instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (OperationMode == WmiOperationMode.Synchronous)
        {
            await ExecuteSyncWithTimeout(() => DeleteInstanceSync(instance, cancellationToken), cancellationToken);
        }
        else
        {
            await PerformWmiActionAsync(observer => instance.Delete(observer), cancellationToken);
        }
    }

    /// <summary>
    /// Executes a static WMI method asynchronously on a class
    /// </summary>
    public async Task<ManagementBaseObject?> ExecuteClassMethodAsync(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default)
    {
        if (OperationMode == WmiOperationMode.Synchronous)
            return await ExecuteSyncWithTimeout(() => ExecuteClassMethodSync(managementClass, methodName, inputParameters, cancellationToken), cancellationToken);
        else
            return await ExecuteWmiMethodAsync(
                observer => managementClass.InvokeMethod(observer, methodName, inputParameters, null),
                cancellationToken);
    }

    /// <summary>
    /// Executes a WMI method asynchronously on an instance
    /// </summary>
    public async Task<ManagementBaseObject?> ExecuteInstanceMethodAsync(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default)
    {
        if (OperationMode == WmiOperationMode.Synchronous)
            return await ExecuteSyncWithTimeout(() => ExecuteInstanceMethodSync(instance, methodName, inputParameters, cancellationToken), cancellationToken);
        else
            return await ExecuteWmiMethodAsync(
                observer => instance.InvokeMethod(observer, methodName, inputParameters, null),
                cancellationToken);
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
        bool excludeLDAP = true,
        CancellationToken cancellationToken = default,
        Action<string, int>? progressCallback = null)
    {
        return Task.Run(async () =>
        {
            EnsureScopeConnected(scope);
            var results = new List<(object match, ManagementBaseObject parent)>();

            // Search in the current namespace
            var currentNamespace = scope.Path?.Path ?? "root";
            var friendlyNamespace = WmiServiceHelpers.FormatNamespaceForDisplay(currentNamespace);
            var failureCount = 0;
            progressCallback?.Invoke($"Searching namespace: {friendlyNamespace}", failureCount);
            await SearchInNamespaceAsync(scope, searchType, searchText, results, cancellationToken);

            // If recursive search is enabled, search in child namespaces
            if (recursive && !cancellationToken.IsCancellationRequested)
            {
                failureCount = await SearchInChildNamespacesRecursivelyAsync(scope, searchType, searchText, results, excludeLDAP, cancellationToken, progressCallback, failureCount);
            }

            return (IEnumerable<(object, ManagementBaseObject)>)results;
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a WMI query asynchronously, using the specified query string.
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> ExecuteWmiQueryAsync(
        ManagementScope scope,
        string queryString,
        bool directRead,
        bool useAmendedQualifiers,
        bool enableLogging = true,
        CancellationToken cancellationToken = default)
    {
        // Log.Debug("Executing WMI query: {Query} on scope: {Scope}", queryString, scope.Path?.Path ?? "Unknown");

        try
        {
            EnsureScopeConnected(scope);
            var userProvidedEnumOptions = new EnumerationOptions
            {
                DirectRead = directRead,
                UseAmendedQualifiers = useAmendedQualifiers,
                EnsureLocatable = true,
                EnumerateDeep = true
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
            if (enableLogging)
            {
                Log.Debug("[{OperationMode}] WMI query '{Query}' completed successfully. Returned {ResultCount} objects", OperationMode, queryString, resultCount);
            }
            return results;
        }
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            if (enableLogging)
            {
                Log.Error(mex, "[{OperationMode}] ManagementException executing WMI query: {Query} on scope: {Scope} - {ErrorMessage}", OperationMode, queryString, scope.Path?.Path ?? "Unknown", wmiException.Message);
            }
            throw wmiException;
        }
        catch (Exception ex)
        {
            if (enableLogging)
            {
                Log.Error(ex, "[{OperationMode}] Failed to execute WMI query: {Query} on scope: {Scope}", OperationMode, queryString, scope.Path?.Path ?? "Unknown");
            }
            throw;
        }
    }

    /// <summary>
    /// Executes a WMI query asynchronously, with optional caching of class metadata if cacheResults is true.
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> ExecuteWmiQueryAsync(
        ManagementScope scope,
        string queryString,
        bool directRead,
        bool useAmendedQualifiers,
        bool cacheResults,
        bool enableLogging = true,
        CancellationToken cancellationToken = default)
    {
        var results = await ExecuteWmiQueryAsync(scope, queryString, directRead, useAmendedQualifiers, enableLogging, cancellationToken);
        if (cacheResults && scope?.Path?.Path is string nsPath && !string.IsNullOrWhiteSpace(nsPath))
        {
            await CacheNamespaceClassMetadata(nsPath, results);
        }
        return results;
    }

    /// <summary>
    /// Gets a cached provider instance for a specific class in a namespace
    /// </summary>
    /// <param name="namespacePath">The namespace path</param>
    /// <param name="className">The class name</param>
    /// <param name="classObject">The ManagementBaseObject representing the class</param>
    /// <returns>The provider instance if found, null otherwise</returns>
    public ManagementObject? GetCachedProviderForClass(string namespacePath, string className, ManagementBaseObject classObject)
    {
        if (string.IsNullOrEmpty(namespacePath) || string.IsNullOrEmpty(className) || classObject?.Qualifiers == null)
            return null;

        try
        {
            var providerQualifier = classObject.Qualifiers.Cast<QualifierData>()
                .FirstOrDefault(q => string.Equals(q.Name, "provider", StringComparison.OrdinalIgnoreCase));

            if (providerQualifier?.Value is string providerName && !string.IsNullOrEmpty(providerName))
            {
                lock (_providerCacheLock)
                {
                    if (_providerCache.TryGetValue(namespacePath, out var providers) &&
                        providers.TryGetValue(providerName, out var providerInstance))
                    {
                        return providerInstance;
                    }
                }
            }
        }
        catch
        {
            // Return null if there's any error accessing qualifiers
        }

        return null;
    }

    /// <summary>
    /// Executes a WMI query to enumerate child namespaces (instances of __namespace).
    /// </summary>
    public async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteWmiQueryAsync(
            scope,
            "SELECT * FROM __namespace",
            directRead: false,
            useAmendedQualifiers: false,
            enableLogging: false,
            cancellationToken);

        // Preload providers for the current namespace to improve performance (after core functionality)
        // Only call if not already cached to avoid duplicate logging
        var namespacePath = scope.Path?.Path ?? string.Empty;
        lock (_providerCacheLock)
        {
            if (!_providerCache.ContainsKey(namespacePath))
            {
                _ = CacheProviderInstancesAsync(scope, cancellationToken);
            }
        }

        return result;
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
            var result = await Task.Run(() => GetManagementObject(namespacePath, connectionOptions, cancellationToken), cancellationToken);

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
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error(mex, "ManagementException connecting to WMI namespace: {NamespacePath} - {ErrorMessage}", namespacePath, wmiException.Message);
            throw wmiException;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error connecting to WMI namespace: {NamespacePath}", namespacePath);
            throw;
        }
    }

    public async Task<ManagementClass?> RefreshClassAsync(ManagementClass managementClass, CancellationToken cancellationToken = default)
    {
        if (managementClass == null)
            throw new ArgumentNullException(nameof(managementClass));

        if (OperationMode == WmiOperationMode.Synchronous)
        {
            return await ExecuteSyncWithTimeout(() => RefreshClassSync(managementClass, cancellationToken), cancellationToken);
        }
        else
        {
            var success = await PerformWmiActionAsync(observer => managementClass.Get(observer), cancellationToken);
            return success ? managementClass : null;
        }
    }

    /// <summary>
    /// Refreshes a WMI instance asynchronously.
    /// </summary>
    public async Task<ManagementObject?> RefreshInstanceAsync(ManagementObject instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (OperationMode == WmiOperationMode.Synchronous)
        {
            return await ExecuteSyncWithTimeout(() => RefreshInstanceSync(instance, cancellationToken), cancellationToken);
        }
        else
        {
            var success = await PerformWmiActionAsync(observer => instance.Get(observer), cancellationToken);
            return success ? instance : null;
        }
    }

    /// <summary>
    /// Saves (creates or updates) a WMI instance synchronously, regardless of OperationMode.
    /// This avoids WMI provider timing/caching issues that can occur with async Put, ensuring
    /// the instance is immediately available for querying and a fully loaded object is returned.
    /// </summary>
    public async Task<ManagementObject?> SaveInstanceAsync(ManagementObject instance, PutType putType = PutType.UpdateOrCreate, CancellationToken cancellationToken = default)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var options = new PutOptions { Type = putType };
        // Always use the synchronous Put for reliability:
        // - Async Put does not guarantee the new instance is immediately available for Get()
        // - Some WMI providers delay visibility or do not update the in-memory object after async Put
        // - This ensures the returned object is fully loaded and ready for use
        return await ExecuteSyncWithTimeout(() =>
        {
            try
            {
                var path = instance.Put(options); // Synchronous Put returns the new ManagementPath
                var updated = new ManagementObject(instance.Scope, path, null);
                Log.Debug("Saved instance with path: {Path}", path?.Path!);
                // Immediately refresh to get all properties (including system-generated keys)
                return RefreshInstanceSync(updated, cancellationToken);
            }
            catch (ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                Log.Error(mex, "Error saving WMI instance: {ErrorMessage}", wmiException.Message);
                throw wmiException;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error saving WMI instance");
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Tries to get a ManagementClass for the specified class name, returning null if not found.
    /// </summary>
    public Task<ManagementClass?> TryGetManagementClassAsync(string namespacePath, ConnectionOptions options, string className, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var scope = CreateManagementScope(namespacePath, options);
                var managementClass = new ManagementClass(scope, new ManagementPath(className), null);
                // Try to access properties to verify existence
                var _ = managementClass.Properties;
                return managementClass;
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~WmiService()
    {
        Dispose(false);
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
    /// Synchronous helper for deleting a WMI instance (calls Delete()).
    /// </summary>
    private bool DeleteInstanceSync(ManagementObject instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            instance.Delete();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting WMI instance");
            throw;
        }
    }

    /// <summary>
    /// Ensures that a ManagementScope is connected
    /// </summary>
    private void EnsureScopeConnected(ManagementScope scope)
    {
        if (!scope.IsConnected)
        {
            try
            {
                // Check if we're on the UI thread and if dispatcher processing is available
                if (Application.Current?.Dispatcher != null &&
                    Application.Current.Dispatcher.CheckAccess() &&
                    !Dispatcher.CurrentDispatcher.HasShutdownStarted)
                {
                    try
                    {
                        // Try to use async approach with dispatcher frame only if safe
                        var connectTask = Task.Run(() => scope.Connect());

                        // Use a dispatcher frame approach to keep the UI responsive while waiting
                        var frame = new DispatcherFrame();

                        connectTask.ContinueWith(t =>
                        {
                            // Stop the dispatcher frame when the task completes
                            frame.Continue = false;
                        }, TaskScheduler.Default);

                        // This will process UI messages until the frame is stopped
                        // Only if dispatcher processing is not suspended
                        Dispatcher.PushFrame(frame);

                        // Now get the result (should be immediate as task is complete)
                        // Will propagate exceptions if they occurred
                        connectTask.GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException)
                    {
                        // Dispatcher processing is suspended, fall back to direct connect
                        scope.Connect();
                    }
                }
                else
                {
                    // Not on UI thread or dispatcher not available, connect directly
                    scope.Connect();
                }
            }
            catch (ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                Log.Error(wmiException, "WMI Exception while connecting to scope '{ScopePath}': {ErrorMessage}", scope?.Path?.Path ?? "Unknown", wmiException.Message);
                throw wmiException;
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
    /// Executes a static WMI method synchronously on a class
    /// </summary>
    private ManagementBaseObject? ExecuteClassMethodSync(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return managementClass.InvokeMethod(methodName, inputParameters, null);
        }
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error("Error executing WMI static method '{MethodName}' on class: {ErrorMessage}", methodName, wmiException.Message);
            throw wmiException;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing WMI static method '{MethodName}' on class", methodName);
            throw;
        }
    }

    /// <summary>
    /// Executes a WMI method synchronously on an instance
    /// </summary>
    private ManagementBaseObject? ExecuteInstanceMethodSync(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return instance.InvokeMethod(methodName, inputParameters, null);
        }
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error("Error executing WMI instance method '{MethodName}' on instance: {ErrorMessage}", methodName, wmiException.Message);
            throw wmiException;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing WMI instance method '{MethodName}' on instance", methodName);
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
            catch (AggregateException ex) when (ex.InnerException is ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                throw wmiException;
            }
            catch (AggregateException ex)
            {
                // Unwrap other exceptions
                throw ex.InnerException ?? ex;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Generalized async WMI method execution for both instance and static methods.
    /// </summary>
    private async Task<ManagementBaseObject?> ExecuteWmiMethodAsync(
        Action<ManagementOperationObserver> invokeMethod,
        CancellationToken cancellationToken)
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
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            }
            else if (e.Status == ManagementStatus.CallCanceled || e.Status == ManagementStatus.OperationCanceled)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            else
            {
                tcs.TrySetException(new WmiException(e.StatusObject, e.Status));
            }
        };

        using (cancellationToken.Register(() =>
        {
            if (!cancelCalled)
            {
                cancelCalled = true;
                Task.Run(() =>
                {
                    try { observer.Cancel(); }
                    catch (Exception ex) { Log.Warning(ex, "Error canceling WMI method observer"); }
                });
            }
        }))
        {
            try
            {
                invokeMethod(observer);
            }
            catch (ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                Log.Error(mex, "WMI error in ExecuteWmiMethodAsync: {ErrorMessage}", wmiException.Message);
                tcs.TrySetException(wmiException);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in ExecuteWmiMethodAsync");
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
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
    /// Gets a ManagementObject for a given namespace path (synchronous helper)
    /// </summary>
    private ManagementObject? GetManagementObject(string namespacePath, ConnectionOptions connectionOptions, CancellationToken cancellationToken)
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
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error("Error getting management object for '{NamespacePath}': {ErrorMessage}", namespacePath, wmiException.Message);
            throw wmiException;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting management object: {NamespacePath}", namespacePath);
            throw;
        }
    }

    /// <summary>
    /// Helper for async WMI actions that do not return a result (e.g., Put, Delete)
    /// </summary>
    private async Task<bool> PerformWmiActionAsync(Action<ManagementOperationObserver> startAction, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        var observer = new ManagementOperationObserver();
        var cancelCalled = false;

        observer.Completed += (s, e) =>
        {
            if (e.Status == ManagementStatus.NoError)
                tcs.TrySetResult(true);
            else if (e.Status == ManagementStatus.CallCanceled || e.Status == ManagementStatus.OperationCanceled)
                tcs.TrySetCanceled(cancellationToken);
            else
            {
                tcs.TrySetException(new WmiException(e.StatusObject, e.Status));
            }
        };

        using (cancellationToken.Register(() =>
        {
            if (!cancelCalled)
            {
                cancelCalled = true;
                Task.Run(() =>
                {
                    try { observer.Cancel(); }
                    catch (Exception ex) { Log.Warning(ex, "Error canceling WMI observer"); }
                });
            }
        }))
        {
            try
            {
                startAction(observer);
            }
            catch (ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                Log.Error(mex, "WMI error in PerformWmiActionAsync: {ErrorMessage}", wmiException.Message);
                tcs.TrySetException(wmiException);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in PerformWmiActionAsync");
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
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
                tcs.TrySetException(new WmiException(e.StatusObject, e.Status));
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
            catch (ManagementException mex)
            {
                var wmiException = new WmiException(mex);
                Log.Error(mex, "WMI error in PerformWmiOperationAsync: {ErrorMessage}", wmiException.Message);
                tcs.TrySetException(wmiException);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in PerformWmiOperationAsync");
                tcs.TrySetException(ex);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Synchronous helper for refreshing a WMI class (calls Get()). Returns the class if successful, null if any error occurs.
    /// </summary>
    private ManagementClass? RefreshClassSync(ManagementClass managementClass, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            managementClass.Get();
            return managementClass;
        }
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error(mex, "Error refreshing WMI class {ClassName}: {ErrorMessage}", managementClass.Path?.ClassName ?? "Unknown", wmiException.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing WMI class {ClassName}", managementClass.Path?.ClassName ?? "Unknown");
            return null;
        }
    }

    /// <summary>
    /// Synchronous helper for refreshing a WMI instance (calls Get()). Returns the instance if successful, null if any error occurs.
    /// </summary>
    private ManagementObject? RefreshInstanceSync(ManagementObject instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            instance.Get();
            return instance;
        }
        catch (ManagementException mex)
        {
            var wmiException = new WmiException(mex);
            Log.Error(mex, "Error refreshing WMI instance {Path}: {ErrorMessage}", instance.Path?.Path ?? "Unknown", wmiException.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing WMI instance {Path}", instance.Path?.Path ?? "Unknown");
            return null;
        }
    }

    /// <summary>
    /// Recursively searches in child namespaces
    /// </summary>
    private async Task<int> SearchInChildNamespacesRecursivelyAsync(
        ManagementScope parentScope,
        WmiSearchType searchType,
        string searchText,
        List<(object match, ManagementBaseObject parent)> results,
        bool excludeLDAP,
        CancellationToken cancellationToken,
        Action<string, int>? progressCallback = null,
        int currentFailureCount = 0)
    {
        var failureCount = currentFailureCount;

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

                    // Format the namespace for display/exclusion check (used for both purposes)
                    var friendlyChildNamespace = WmiServiceHelpers.FormatNamespaceForDisplay(childNamespacePath);

                    // Skip the specific LDAP namespace if excludeLDAP is enabled
                    if (excludeLDAP)
                    {
                        var relativePath = friendlyChildNamespace.ToLowerInvariant();
                        if (relativePath == "root\\directory\\ldap")
                        {
                            Log.Warning("Skipping LDAP namespace: {NamespacePath}", friendlyChildNamespace);
                            continue;
                        }
                    }

                    // Create a new scope for the child namespace
                    var childScope = CreateManagementScope(childNamespacePath, parentScope.Options);

                    // Report progress for the child namespace being searched
                    progressCallback?.Invoke($"Searching namespace: {friendlyChildNamespace}", failureCount);

                    // Search in the child namespace
                    await SearchInNamespaceAsync(childScope, searchType, searchText, results, cancellationToken);

                    // Recursively search in grandchild namespaces
                    failureCount = await SearchInChildNamespacesRecursivelyAsync(childScope, searchType, searchText, results, excludeLDAP, cancellationToken, progressCallback, failureCount);
                }
                catch (Exception ex)
                {
                    // Log the error and increment failure count
                    failureCount++;
                    Log.Warning(ex, "Error searching in child namespace during recursive search (failure #{FailureCount})", failureCount);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error and increment failure count for getting child namespaces
            failureCount++;
            Log.Warning(ex, "Error getting child namespaces for recursive search (failure #{FailureCount})", failureCount);
        }

        return failureCount;
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
            EnumerationOptions enumOptions = new EnumerationOptions
            {
                UseAmendedQualifiers = true,
                DirectRead = false,
                EnsureLocatable = true,
                EnumerateDeep = true
            };

            switch (searchType)
            {
                case WmiSearchType.Class:
                    var classQuery = new SelectQuery("meta_class");
                    using (var searcher = new ManagementObjectSearcher(scope, classQuery, enumOptions))
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
                    using (var searcher = new ManagementObjectSearcher(scope, methodClassQuery, enumOptions))
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
                    using (var searcher = new ManagementObjectSearcher(scope, propertyClassQuery, enumOptions))
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

    /// <summary>
    /// Custom exception to carry full WMI status object for property grid display
    /// </summary>
    public class WmiException : ManagementException
    {
        public WmiException(ManagementBaseObject? errorDetails, ManagementStatus status, Exception? innerException = null)
            : base(WmiServiceHelpers.ExtractWmiErrorInformation(errorDetails, status), innerException)
        {
            ErrorDetails = errorDetails;
        }

        public WmiException(ManagementException mex)
            : base(WmiServiceHelpers.ExtractWmiErrorMessage(mex), mex)
        {
            ErrorDetails = mex.ErrorInformation;
        }

        public ManagementBaseObject? ErrorDetails { get; }
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

                // Clear provider cache
                ClearAllProviderCaches();
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