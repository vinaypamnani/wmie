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
    ManagementScope CreateManagementScope(string namespacePath, ConnectionOptions connectionOptions);
    /// <summary>
    /// Deletes a WMI instance asynchronously.
    /// </summary>
    /// <param name="instance">The ManagementObject instance to delete.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task DeleteInstanceAsync(ManagementObject instance, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes a static WMI method asynchronously on a class
    /// </summary>
    /// <param name="managementClass">The ManagementClass to execute the static method on</param>
    /// <param name="methodName">The name of the method to execute</param>
    /// <param name="inputParameters">The input parameters for the method (optional)</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>The output parameters from the method execution</returns>
    Task<ManagementBaseObject?> ExecuteClassMethodAsync(ManagementClass managementClass, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes a WMI method asynchronously on an instance
    /// </summary>
    /// <param name="instance">The ManagementObject instance to execute the method on</param>
    /// <param name="methodName">The name of the method to execute</param>
    /// <param name="inputParameters">The input parameters for the method (optional)</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>The output parameters from the method execution</returns>
    Task<ManagementBaseObject?> ExecuteInstanceMethodAsync(ManagementObject instance, string methodName, ManagementBaseObject? inputParameters = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes a search for classes, methods, or properties in the given scope.
    /// </summary>
    /// <param name="scope">The ManagementScope to use for the query</param>
    /// <param name="searchType">The type of search (Class, Method, Property)</param>
    /// <param name="searchText">The search text to filter results</param>
    /// <param name="recursive">Whether to search recursively</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="progressCallback">Optional callback to report search progress (namespace being searched, failure count)</param>
    /// <returns>A list of tuples where first item is the search match (ManagementClass, MethodData, or PropertyData) and second is the parent class</returns>
    Task<IEnumerable<(object match, ManagementBaseObject parent)>> ExecuteSearchAsync(
        ManagementScope scope,
        WmiSearchType searchType,
        string searchText,
        bool recursive,
        bool excludeLDAP = true,
        CancellationToken cancellationToken = default,
        Action<string, int>? progressCallback = null);
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
    /// Executes a WMI query asynchronously, with optional caching of class metadata if cacheResults is true.
    /// </summary>
    /// <param name="scope">The ManagementScope to use for the query</param>
    /// <param name="queryString">The WQL query string to execute</param>
    /// <param name="directRead">Whether to use direct read</param>
    /// <param name="useAmendedQualifiers">Whether to use amended qualifiers</param>
    /// <param name="cacheResults">If true, caches class metadata for the given namespace (only for class queries)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of ManagementObject representing the query results</returns>
    Task<IEnumerable<ManagementObject>> ExecuteWmiQueryAsync(
        ManagementScope scope,
        string queryString,
        bool directRead,
        bool useAmendedQualifiers,
        bool cacheResults,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Enumerates child namespaces for a given WMI namespace (instances of __namespace).
    /// </summary>
    Task<IEnumerable<ManagementObject>> GetChildNamespacesAsync(ManagementScope scope, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the CLSID for a WMI provider by name (synchronous, returns null if not found or error)
    /// </summary>
    string? GetProviderClsid(ManagementScope scope, string providerName);
    /// <summary>
    /// Gets a root ManagementObject for a given namespace path
    /// </summary>
    /// <param name="namespacePath">The full path of the namespace</param>
    /// <param name="connectionOptions">Connection options to use</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A ManagementObject</returns>
    Task<ManagementObject?> GetRootNamespaceAsync(string namespacePath, ConnectionOptions connectionOptions, CancellationToken cancellationToken = default);
    /// <summary>
    /// Attempts to get a ManagementClass for a given class name in the specified namespace. Returns null if not found or not accessible.
    /// </summary>
    Task<ManagementClass?> TryGetManagementClassAsync(string namespacePath, ConnectionOptions options, string className, CancellationToken cancellationToken = default);
}