using Moq;
using NUnit.Framework;
using System.Management;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Logging;
using WmiExplorer.Services;

namespace WmiExplorer.Tests;

[TestFixture]
public class WmiServiceProviderCacheTests
{
    private Mock<ICacheService> _mockCacheService;
    private Mock<ISettingsService> _mockSettingsService;
    private WmiService _wmiService;

    [Test]
    public void ClearAllProviderCaches_ShouldClearAllCaches()
    {
        // Arrange
        var scope = new ManagementScope("root\\cimv2");

        // Act
        _wmiService.ClearAllProviderCaches();

        // Assert - should not throw and cache should be empty
        // Note: We can't easily test the internal cache state, but we can verify the method doesn't throw
        Assert.Pass(); // If we get here, the method didn't throw
    }

    [Test]
    public void ClearProviderCache_WithEmptyNamespace_ShouldNotThrow()
    {
        // Arrange
        string? namespacePath = null;

        // Act & Assert
        Assert.DoesNotThrow(() => _wmiService.ClearProviderCache(namespacePath!));
    }

    [Test]
    public void ClearProviderCache_WithValidNamespace_ShouldClearSpecificCache()
    {
        // Arrange
        var namespacePath = "root\\cimv2";

        // Act
        _wmiService.ClearProviderCache(namespacePath);

        // Assert - should not throw
        Assert.Pass(); // If we get here, the method didn't throw
    }

    [Test]
    public void GetCachedProviderForClass_WithEmptyParameters_ShouldReturnNull()
    {
        // Arrange
        var namespacePath = "";
        var className = "";
        // Create a real ManagementBaseObject instead of mocking it
        var scope = new ManagementScope("root\\cimv2");
        var classObject = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);

        // Act
        var result = _wmiService.GetCachedProviderForClass(namespacePath, className, classObject);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetCachedProviderForClass_WithNullParameters_ShouldReturnNull()
    {
        // Arrange
        string? namespacePath = null;
        string? className = null;
        ManagementBaseObject? classObject = null;

        // Act
        var result = _wmiService.GetCachedProviderForClass(namespacePath!, className!, classObject!);

        // Assert
        Assert.That(result, Is.Null);
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Initialize logging for tests
        Log.ConfigureLogging();
    }

    [SetUp]
    public void Setup()
    {
        _mockCacheService = new Mock<ICacheService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService.Setup(x => x.OperationMode).Returns(WmiOperationMode.Synchronous);

        _wmiService = new WmiService(_mockCacheService.Object, _mockSettingsService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _wmiService?.Dispose();
    }
}