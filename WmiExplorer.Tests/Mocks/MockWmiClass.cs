using WmiExplorer.Common.Cache;

namespace WmiExplorer.Tests.Mocks;

/// <summary>
/// Mock implementation of WmiClassCache for testing
/// </summary>
public class MockWmiClassCache : WmiClassCache
{
    // WmiClassCache already has most of what we need
    // Just adding a few convenience methods

    public void AddProperty(string name, string type)
    {
        var property = new MockWmiPropertyCache
        {
            Name = name,
            Type = type
        };

        Properties.Add(property);
    }
}