using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Providers;

/// <summary>
/// This module handles registration of all WMI-specific providers and converters.
/// It serves as the single entry point for all WMI functionality in the PropertyGrid,
/// enabling clean separation of WMI-specific code from the generic PropertyGrid implementation.
/// </summary>
public static class ProviderModule
{
    private static readonly PropertyTypeProviderRegistry registry = PropertyTypeProviderRegistry.Instance;

    /// <summary>
    /// Registers a custom property type provider and optional value converter.
    /// </summary>
    /// <param name="provider">The property type provider to register.</param>
    /// <param name="converter">Optional value converter to register.</param>
    public static void RegisterProvider(IPropertyTypeProvider provider, IPropertyValueConverter? converter = null)
    {
        registry.RegisterProvider(provider);
        if (converter != null)
        {
            registry.RegisterConverter(converter);
        }
        System.Diagnostics.Debug.WriteLine($"{provider.GetType().Name} registered{(converter != null ? " with converter" : "")}");
    }

    /// <summary>
    /// Unregisters a custom property type provider and optional value converter.
    /// </summary>
    /// <param name="provider">The property type provider to unregister.</param>
    /// <param name="converter">Optional value converter to unregister.</param>
    public static void UnregisterProvider(IPropertyTypeProvider provider, IPropertyValueConverter? converter = null)
    {
        registry.UnregisterProvider(provider);
        if (converter != null)
        {
            registry.UnregisterConverter(converter);
        }
        System.Diagnostics.Debug.WriteLine($"{provider.GetType().Name} unregistered{(converter != null ? " with converter" : "")}");
    }
}