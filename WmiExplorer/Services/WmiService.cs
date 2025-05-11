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
        /// Gets the display name for an instance
        /// </summary>
        private string GetInstanceName(ManagementObject instanceObject, string className)
        {
            try
            {
                // Different WMI classes have different identifying properties
                // Try to get a meaningful name from common identifying properties
                string[] nameProperties = { "Name", "Caption", "DeviceID", "InstanceName", "ProcessId" };

                foreach (var prop in nameProperties)
                {
                    var property = instanceObject.Properties[prop];
                    if (property?.Value != null)
                    {
                        return property.Value.ToString() ?? $"{className}_Property";
                    }
                }

                // Fall back to the class name with a unique identifier
                return $"{className}_{Guid.NewGuid().ToString()[..8]}";
            }
            catch
            {
                return $"{className}_{Guid.NewGuid().ToString()[..8]}";
            }
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
        /// Creates a connected ManagementScope for a namespace path
        /// </summary>
        public ManagementScope CreateManagementScope(string namespacePath)
        {
            var scope = new ManagementScope(namespacePath);
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
        public async Task<IEnumerable<WmiNamespace>> GetChildNamespacesAsync(string namespacePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<WmiNamespace>();
                var scope = CreateManagementScope(namespacePath);

                var nsClass = new ManagementClass(scope, new ManagementPath("__namespace"), null);
                _disposables.Add(nsClass);
                var instances = nsClass.GetInstances();
                _disposables.Add(instances);

                foreach (ManagementObject m in instances)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _disposables.Add(m);
                    var childName = m["Name"]?.ToString() ?? "";
                    var fullPath = $"{namespacePath}\\{childName}";
                    
                    // Create simplified WmiNamespace just with ActualObject and FullPath
                    result.Add(new WmiNamespace(m, fullPath));
                }

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets classes for a given WMI namespace
        /// </summary>
        public async Task<IEnumerable<WmiClass>> GetClassesAsync(string namespacePath, WmiClassTypeFlags classTypeFilter = WmiClassTypeFlags.All, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<WmiClass>();

                var scope = CreateManagementScope(namespacePath);

                // Build the WQL query based on the class type filter
                string queryString = BuildClassQueryFromFilter(classTypeFilter);
                var query = new ObjectQuery(queryString);                
                var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
                _disposables.Add(searcher);

                foreach (ManagementObject classObject in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _disposables.Add(classObject);
                    string? className = classObject["__Class"]?.ToString();
                    if (className != null)
                    {
                        result.Add(new WmiClass(className, namespacePath, classObject));
                    }
                }

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets instances for a given WMI class
        /// </summary>
        public async Task<IEnumerable<WmiInstance>> GetInstancesAsync(string namespacePath, string className, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<WmiInstance>();

                var scope = CreateManagementScope(namespacePath);
                var query = new ObjectQuery($"SELECT * FROM {className}");                
                var searcher = new ManagementObjectSearcher(scope, query, _enumOptions);
                _disposables.Add(searcher);

                foreach (ManagementObject instanceObject in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _disposables.Add(instanceObject);
                    string instanceName = GetInstanceName(instanceObject, className);
                    result.Add(new WmiInstance(instanceName, instanceObject));
                }

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets a root WmiNamespace object for a given namespace path
        /// </summary>
        public async Task<WmiNamespace> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // Create a direct ManagementObject for the root namespace
                    ManagementPath mPath = new ManagementPath(namespacePath);
                    ManagementScope mScope = new ManagementScope(mPath);
                    mScope.Connect();
                    
                    // Create ManagementObject for the namespace
                    ObjectGetOptions oOptions = new ObjectGetOptions();
                    ManagementObject mObject = new ManagementObject(mScope, mPath, oOptions);
                    _disposables.Add(mObject);
                    
                    // Create WmiNamespace with the root flag set to true
                    var rootNamespace = new WmiNamespace(mObject, namespacePath);
                    rootNamespace.IsRoot = true;
                    
                    return rootNamespace;
                }
                catch (Exception ex)
                {
                    // Log the error and return a basic namespace
                    System.Diagnostics.Debug.WriteLine($"Error creating root namespace: {ex.Message}");
                    var rootNamespace = new WmiNamespace(null, namespacePath);
                    rootNamespace.IsRoot = true;
                    return rootNamespace;
                }
            }, cancellationToken);
        }
    }
}