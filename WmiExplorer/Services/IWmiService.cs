using System.Management;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Services;

public interface IWmiService
{
    /// <summary>
    /// Gets or sets the operation mode for WMI service
    /// </summary>
    WmiOperationMode OperationMode { get; set; }

    /// <summary>
    /// Creates a ManagementScope for a given namespace path and optional connection options
    /// </summary>
    /// <param name="namespacePath">The namespace path</param>
    /// <param name="options">The connection options (optional)</param>
    /// <returns>A connected ManagementScope</returns>
    ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions? options = null);
    /// <summary>
    /// Executes a search for classes, methods, or properties in the given scope.
    /// </summary>
    /// <param name="scope">The ManagementScope to use for the query</param>
    /// <param name="searchType">The type of search (Class, Method, Property)</param>
    /// <param name="searchText">The search text to filter results</param>
    /// <param name="recursive">Whether to search recursively</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of tuples where first item is the search match (ManagementClass, MethodData, or PropertyData) and second is the parent class</returns>
    Task<IEnumerable<(object match, ManagementBaseObject parent)>> ExecuteSearchAsync(
        ManagementScope scope,
        WmiSearchType searchType,
        string searchText,
        bool recursive,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes a WMI query asynchronously, using the specified query string and enumeration options.
    /// </summary>
    /// <param name="scope">The ManagementScope to use for the query</param>
    /// <param name="queryString">The WQL query string to execute</param>
    /// <param name="enumerateDeep">Whether to enumerate deep</param>
    /// <param name="useAmendedQualifiers">Whether to use amended qualifiers</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A list of ManagementObject representing the query results</returns>
    Task<IEnumerable<ManagementObject>> ExecuteWmiQueryAsync(ManagementScope scope, string queryString, bool directRead, bool useAmendedQualifiers, CancellationToken cancellationToken = default);
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
    /// Gets the CLSID for a WMI provider by name (synchronous, returns null if not found or error)
    /// </summary>
    string? GetProviderClsid(ManagementScope scope, string providerName);
    /// <summary>
    /// Gets a root ManagementObject for a given namespace path
    /// </summary>
    /// <param name="namespacePath">The full path of the namespace</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A ManagementObject</returns>
    Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, CancellationToken cancellationToken = default);
}