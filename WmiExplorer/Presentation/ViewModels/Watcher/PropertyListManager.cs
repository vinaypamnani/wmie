using System.Collections.ObjectModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Watcher;

/// <summary>
/// Manages a collection of PropertyDisplayInfo items with support for async loading from cache or in-memory classes
/// </summary>
public class PropertyListManager : DisposableObservableObject
{
    private readonly ICacheService _cacheService;
    private readonly ObservableCollection<PropertyDisplayInfo> _properties = new();

    /// <summary>
    /// Initializes a new instance of the PropertyListManager class
    /// </summary>
    /// <param name="cacheService">The cache service for loading properties</param>
    public PropertyListManager(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        Properties = new ReadOnlyObservableCollection<PropertyDisplayInfo>(_properties);
    }

    /// <summary>
    /// Gets the read-only collection of properties
    /// </summary>
    public ReadOnlyObservableCollection<PropertyDisplayInfo> Properties { get; }

    /// <summary>
    /// Clears all properties from the collection
    /// </summary>
    public void Clear()
    {
        _properties.Clear();
    }

    /// <summary>
    /// Finds and returns the preferred property from the current collection
    /// </summary>
    /// <param name="preferredNames">Collection of preferred property name patterns</param>
    /// <returns>The preferred property or the first property if none match</returns>
    public PropertyDisplayInfo? GetPreferredProperty(IEnumerable<Func<PropertyDisplayInfo, bool>> preferredMatchers)
    {
        if (_properties.Count == 0)
            return null;

        // Try to find a property that matches any of the preferred matchers
        foreach (var matcher in preferredMatchers)
        {
            var match = _properties.FirstOrDefault(matcher);
            if (match != null)
                return match;
        }

        // If no preferred property found, use the first property
        return _properties.FirstOrDefault();
    }

    /// <summary>
    /// Updates the properties collection for the specified class
    /// </summary>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <param name="className">The class name to load properties for</param>
    /// <returns>A task representing the async operation</returns>
    public async Task UpdatePropertiesAsync(WmiNamespaceViewModel? selectedNamespace, string? className)
    {
        _properties.Clear();

        if (selectedNamespace == null || string.IsNullOrEmpty(className))
            return;

        var properties = await GetClassPropertiesAsync(selectedNamespace, className);
        foreach (var prop in properties)
            _properties.Add(prop);
    }

    /// <summary>
    /// Disposes the manager and clears the properties collection
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _properties.Clear();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Gets properties for the specified class from in-memory classes or cache
    /// </summary>
    /// <param name="selectedNamespace">The selected namespace view model</param>
    /// <param name="className">The class name to load properties for</param>
    /// <returns>A collection of property display info</returns>
    private async Task<IEnumerable<PropertyDisplayInfo>> GetClassPropertiesAsync(WmiNamespaceViewModel selectedNamespace, string className)
    {
        // Prefer in-memory class properties if available
        var inMemoryClass = selectedNamespace.Classes?.FirstOrDefault(c => c.ClassName == className);
        if (inMemoryClass?.WmiClass?.Properties != null && inMemoryClass.WmiClass.Properties.Count > 0)
        {
            return inMemoryClass.WmiClass.Properties
                .Cast<System.Management.PropertyData>()
                .Where(p => !IsExcludedProperty(p.Name))
                .Select(p => new PropertyDisplayInfo
                {
                    Name = p.Name,
                    Type = p.Type.ToString() ?? string.Empty
                });
        }

        // Fall back to cache
        try
        {
            var cachedProperties = await _cacheService.GetPropertiesForClassAsync(selectedNamespace.NamespacePath, className);
            return cachedProperties
                .Where(p => !IsExcludedProperty(p.Name))
                .Select(p => new PropertyDisplayInfo
                {
                    Name = p.Name,
                    Type = p.Type ?? string.Empty
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
            return Enumerable.Empty<PropertyDisplayInfo>();
        }
    }

    /// <summary>
    /// Determines if a property should be excluded from the list
    /// </summary>
    /// <param name="propertyName">The property name to check</param>
    /// <returns>True if the property should be excluded</returns>
    private static bool IsExcludedProperty(string propertyName) =>
        string.Equals(propertyName, "TIME_CREATED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(propertyName, "SECURITY_DESCRIPTOR", StringComparison.OrdinalIgnoreCase);
}