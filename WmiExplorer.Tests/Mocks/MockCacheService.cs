using WmiExplorer.Common.Cache;
using WmiExplorer.Services;

namespace WmiExplorer.Tests.Mocks;

/// <summary>
/// Mock implementation of ICacheService for testing completion functionality.
/// </summary>
public class MockCacheService : ICacheService
{
    // Holds mock namespace cache data.
    private readonly List<MockNamespaceCache> _namespaceCache = new();

    public MockCacheService()
    {
        InitializeMockData();
    }

    /// <summary>
    /// Retrieves a namespace cache by path.
    /// </summary>
    public Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath) =>
        Task.FromResult<WmiNamespaceCache?>(
            _namespaceCache.FirstOrDefault(nc =>
                string.Equals(nc.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase))
        );

    /// <summary>
    /// Retrieves all class names for a given namespace.
    /// </summary>
    public Task<List<WmiClassCache>> GetClassesForNamespaceAsync(string namespacePath)
    {
        var namespaceCache = _namespaceCache.FirstOrDefault(nc =>
            string.Equals(nc.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase));

        var classNames = namespaceCache?.Classes ?? new List<WmiClassCache>();
        return Task.FromResult(classNames);
    }

    /// <summary>
    /// Retrieves all property names for a given class in a namespace.
    /// </summary>
    public Task<List<WmiPropertyCache>> GetPropertiesForClassAsync(string namespacePath, string className)
    {
        var namespaceCache = _namespaceCache.FirstOrDefault(nc =>
            string.Equals(nc.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase));

        var classCache = namespaceCache?.Classes
            .FirstOrDefault(c => string.Equals(c.ClassName, className, StringComparison.OrdinalIgnoreCase));

        var propertyNames = classCache?.Properties ?? new List<WmiPropertyCache>();
        return Task.FromResult(propertyNames);
    }

    /// <summary>
    /// Loads all namespace caches.
    /// </summary>
    public Task<List<WmiNamespaceCache>> LoadCacheAsync() =>
        Task.FromResult(_namespaceCache.Cast<WmiNamespaceCache>().ToList());

    /// <summary>
    /// Updates or adds a namespace cache.
    /// </summary>
    public Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
    {
        if (namespaceCache is not MockNamespaceCache mockNamespace)
            throw new ArgumentException("Invalid cache type for MockCacheService.", nameof(namespaceCache));

        var existingIndex = _namespaceCache.FindIndex(nc =>
            string.Equals(nc.NamespacePath, mockNamespace.NamespacePath, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            _namespaceCache[existingIndex] = mockNamespace;
        else
            _namespaceCache.Add(mockNamespace);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initialize mock data using a data-driven approach.
    /// </summary>
    private void InitializeMockData()
    {
        var mockClasses = new[]
        {
            new {
                Name = "Win32_Process",
                Properties = new[] { "Name:string", "ProcessId:uint32", "ExecutablePath:string", "CommandLine:string", "DummyBool:boolean" }
            },
            new {
                Name = "Win32_OperatingSystem",
                Properties = new[] { "Caption:string", "Version:string", "BuildNumber:string", "OSArchitecture:string" }
            },
            new {
                Name = "Win32_Service",
                Properties = new[] { "Name:string", "DisplayName:string", "State:string", "StartMode:string" }
            }
        };

        var cimv2 = new MockNamespaceCache
        {
            NamespacePath = "root\\CIMV2",
            LastUpdatedUtc = DateTime.UtcNow
        };

        foreach (var classData in mockClasses)
        {
            var mockClass = new MockWmiClassCache
            {
                ClassName = classData.Name,
                IsSystemClass = false,
                IsEventClass = false
            };

            foreach (var propertyData in classData.Properties)
            {
                var parts = propertyData.Split(':');
                mockClass.AddProperty(parts[0], parts[1]);
            }

            cimv2.Classes.Add(mockClass);
        }

        _namespaceCache.Add(cimv2);
    }
}