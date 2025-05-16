using System.Management;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Services
{
    public interface IWmiService
    {
        /// <summary>
        /// Creates a ManagementScope for a given namespace path and optional connection options
        /// </summary>
        /// <param name="namespacePath">The namespace path</param>
        /// <param name="options">The connection options (optional)</param>
        /// <returns>A connected ManagementScope</returns>
        ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions? options = null);

        /// <summary>
        /// Asynchronously gets child namespaces for a given WMI namespace
        /// </summary>
        /// <param name="scope">The ManagementScope to use for the query</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of ManagementObject</returns>
        Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets classes for a given WMI namespace
        /// </summary>
        /// <param name="scope">The ManagementScope to use for the query</param>
        /// <param name="classTypeFilter">Filter to specify which types of classes to include</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of ManagementObject representing WMI classes</returns>
        Task<IEnumerable<ManagementObject>> GetClassesAsync(ManagementScope scope, WmiClassTypeFlags classTypeFilter = WmiClassTypeFlags.All, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets instances for a given WMI class
        /// </summary>
        /// <param name="scope">The ManagementScope to use for the query</param>
        /// <param name="className">The name of the WMI class</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of ManagementObject representing WMI instances</returns>
        Task<IEnumerable<ManagementObject>> GetInstancesAsync(ManagementScope scope, string className, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a root ManagementObject for a given namespace path
        /// </summary>
        /// <param name="namespacePath">The full path of the namespace</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A ManagementObject</returns>
        Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or sets the operation mode for WMI service
        /// </summary>
        WmiOperationMode OperationMode { get; set; }

        /// <summary>
        /// Gets the CLSID for a WMI provider by name (synchronous, returns null if not found or error)
        /// </summary>
        string? GetProviderClsid(ManagementScope scope, string providerName);
    }
}