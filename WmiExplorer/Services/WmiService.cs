using System.Management;
using WmiExplorer.Common.Shared;
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

            // Add exclusion conditions for unchecked class types
            if ((classTypeFilter & WmiClassTypeFlags.System) != WmiClassTypeFlags.System)
                query += " AND NOT __Class LIKE \"[_][_]%\"";

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
        /// Creates a connected ManagementScope for a namespace path and optional connection options
        /// </summary>
        public ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions? options = null)
        {
            var scope = options != null ? new ManagementScope(namespacePath, options) : new ManagementScope(namespacePath);
            scope.Connect();
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
        /// Asynchronously gets child namespaces for a given WMI namespace
        /// </summary>
        public async Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<ManagementObject>();
                if (!scope.IsConnected)
                    scope.Connect();

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
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets classes for a given WMI namespace
        /// </summary>
        public async Task<IEnumerable<ManagementObject>> GetClassesAsync(ManagementScope scope, WmiClassTypeFlags classTypeFilter = WmiClassTypeFlags.All, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<ManagementObject>();
                if (!scope.IsConnected)
                    scope.Connect();

                // Build the WQL query based on the class type filter
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

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets instances for a given WMI class
        /// </summary>
        public async Task<IEnumerable<ManagementObject>> GetInstancesAsync(ManagementScope scope, string className, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<ManagementObject>();
                if (!scope.IsConnected)
                    scope.Connect();
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
            }, cancellationToken);
        }

        /// <summary>
        /// Gets a root ManagementObject for a given namespace path
        /// </summary>
        public async Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ManagementPath mPath = new ManagementPath(namespacePath);
                    ManagementScope mScope = new ManagementScope(mPath);
                    mScope.Connect();
                    ObjectGetOptions oOptions = new ObjectGetOptions();
                    ManagementObject mObject = new ManagementObject(mScope, mPath, oOptions);                    
                    //_disposables.Add(mObject);
                    return mObject;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating root namespace: {ex.Message}");
                    return null;
                }
            }, cancellationToken);
        }
    }
}