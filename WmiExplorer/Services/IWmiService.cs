using System.Management;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Services
{
    public interface IWmiService
    {
        /// <summary>
        /// Creates a ManagementScope for a given namespace path
        /// </summary>
        /// <param name="namespacePath">The namespace path</param>
        /// <returns>A connected ManagementScope</returns>
        ManagementScope CreateManagementScope(string namespacePath);

        /// <summary>
        /// Asynchronously gets child namespaces for a given WMI namespace
        /// </summary>
        /// <param name="namespacePath">The full path of the parent namespace</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of WMI namespace models</returns>
        Task<IEnumerable<WmiNamespace>> GetChildNamespacesAsync(string namespacePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets classes for a given WMI namespace
        /// </summary>
        /// <param name="namespacePath">The full path of the namespace</param>
        /// <param name="classTypeFilter">Filter to specify which types of classes to include</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of WMI class models</returns>
        Task<IEnumerable<WmiClass>> GetClassesAsync(string namespacePath, WmiClassTypeFlags classTypeFilter = WmiClassTypeFlags.All, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets instances for a given WMI class
        /// </summary>
        /// <param name="namespacePath">The namespace path containing the class</param>
        /// <param name="className">The name of the WMI class</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A list of WMI instance models</returns>
        Task<IEnumerable<WmiInstance>> GetInstancesAsync(string namespacePath, string className, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a root WmiNamespace object for a given namespace path
        /// </summary>
        /// <param name="namespacePath">The full path of the namespace</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>A WmiNamespace model with the actual WMI object</returns>
        Task<WmiNamespace> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default);
    }
}