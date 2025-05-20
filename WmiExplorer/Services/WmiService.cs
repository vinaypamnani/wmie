using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Cache;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Services
{
    public class WmiService : IWmiService, IDisposable
    {
        private readonly List<IDisposable> _disposables = new();
        private bool _disposed;
        private readonly EnumerationOptions _enumOptions = new EnumerationOptions
        {
            UseAmendedQualifiers = true,
            EnumerateDeep = true
        };

        public WmiOperationMode OperationMode { get; set; } = WmiOperationMode.Asynchronous;

        // Cache for provider CLSIDs to avoid repeated WMI queries
        private readonly Dictionary<string, string?> _providerClsidCache = new();
        private readonly ICacheService _cacheService;

        public WmiService(ICacheService cacheService)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
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
        /// Implements IDisposable to clean up WMI resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Helper for async WMI queries returning a list
        /// </summary>
        private async Task<List<ManagementObject>> RunWmiListAsync(Action<ManagementOperationObserver> startAction, CancellationToken cancellationToken)
        {
            var result = new List<ManagementObject>();
            var tcs = new TaskCompletionSource<List<ManagementObject>>();
            var observer = new ManagementOperationObserver();

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
                    tcs.TrySetResult(result);
                else
                    tcs.TrySetException(new ManagementException($"WMI async query failed: {e.Status}"));
            };
            observer.Progress += (sender, e) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    observer.Cancel();
                    tcs.TrySetCanceled(cancellationToken);
                }
            };

            using (cancellationToken.Register(() => {
                observer.Cancel();
                tcs.TrySetCanceled(cancellationToken);
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

        private async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsyncInternal(ManagementScope scope, CancellationToken cancellationToken)
        {
            var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
            _disposables.Add(nsClass);
            return await RunWmiListAsync(obs => nsClass.GetInstances(obs), cancellationToken);
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
                    classCaches.Add(new WmiClassCache
                    {
                        ClassName = className,
                        RelativePath = relativePath,
                        IsSystemClass = isSystem,
                        IsEventClass = isEvent
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

        private async Task<IEnumerable<ManagementObject>> GetInstancesAsyncInternal(ManagementScope scope, string className, CancellationToken cancellationToken)
        {
            var query = new ObjectQuery($"SELECT * FROM {className}");
            var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
            _disposables.Add(searcher);
            return await RunWmiListAsync(obs => searcher.Get(obs), cancellationToken);
        }

        /// <summary>
        /// Gets a root ManagementObject for a given namespace path
        /// </summary>
        public async Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default)
        {
            // Always use sync for root namespace, even in async mode
            return await Task.Run(() => GetRootNamespaceSync(namespacePath, cancellationToken), cancellationToken);
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
    }
}