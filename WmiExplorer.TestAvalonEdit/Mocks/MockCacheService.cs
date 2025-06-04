using WmiExplorer.Core.Cache;
using WmiExplorer.Services;

namespace WmiExplorer.TestAvalonEdit.Mocks;

/// <summary>
/// Mock implementation of ICacheService for testing completion functionality
/// </summary>
public class MockCacheService : ICacheService
{
    // Mock data
    private readonly List<MockNamespaceCache> _namespaceCache = new();

    public MockCacheService()
    {
        // Initialize a namespace
        var cimv2 = new MockNamespaceCache
        {
            NamespacePath = "root\\CIMV2",
            LastUpdatedUtc = DateTime.UtcNow
        };

        // Add Win32_Process class with properties
        var processClass = new MockWmiClassCache
        {
            ClassName = "Win32_Process",
            IsSystemClass = false,
            IsEventClass = false,
            RelativePath = "root\\CIMV2:Win32_Process"
        };

        processClass.AddProperty("Name", "string");
        processClass.AddProperty("ProcessId", "uint32");
        processClass.AddProperty("ExecutablePath", "string");
        processClass.AddProperty("CommandLine", "string");
        processClass.AddProperty("DummyBool", "boolean");

        cimv2.Classes.Add(processClass);

        // Add Win32_OperatingSystem class with properties
        var osClass = new MockWmiClassCache
        {
            ClassName = "Win32_OperatingSystem",
            IsSystemClass = false,
            IsEventClass = false,
            RelativePath = "root\\CIMV2:Win32_OperatingSystem"
        };

        osClass.AddProperty("Caption", "string");
        osClass.AddProperty("Version", "string");
        osClass.AddProperty("BuildNumber", "string");
        osClass.AddProperty("OSArchitecture", "string");

        cimv2.Classes.Add(osClass);

        // Add Win32_Service class with properties
        var serviceClass = new MockWmiClassCache
        {
            ClassName = "Win32_Service",
            IsSystemClass = false,
            IsEventClass = false,
            RelativePath = "root\\CIMV2:Win32_Service"
        };

        serviceClass.AddProperty("Name", "string");
        serviceClass.AddProperty("DisplayName", "string");
        serviceClass.AddProperty("State", "string");
        serviceClass.AddProperty("StartMode", "string");

        cimv2.Classes.Add(serviceClass);

        _namespaceCache.Add(cimv2);
    }

    // Required by ICacheService
    public Task<WmiNamespaceCache?> GetNamespaceCacheAsync(string namespacePath)
    {
        var namespaceCache = _namespaceCache.FirstOrDefault(nc =>
            string.Equals(nc.NamespacePath, namespacePath, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<WmiNamespaceCache?>(namespaceCache);
    }

    public Task<List<WmiNamespaceCache>> LoadCacheAsync()
    {
        return Task.FromResult<List<WmiNamespaceCache>>(_namespaceCache.Cast<WmiNamespaceCache>().ToList());
    }

    public Task UpdateNamespaceCacheAsync(WmiNamespaceCache namespaceCache)
    {
        var existingIndex = _namespaceCache.FindIndex(nc =>
            string.Equals(nc.NamespacePath, namespaceCache.NamespacePath, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            _namespaceCache[existingIndex] = (MockNamespaceCache)namespaceCache;
        else
            _namespaceCache.Add((MockNamespaceCache)namespaceCache);

        return Task.CompletedTask;
    }
}