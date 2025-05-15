using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;
using WmiExplorer.Presentation.Controls.PropertyGrid.Converters;

namespace WmiExplorer.Presentation.Controls.PropertyGrid.Providers
{
    /// <summary>
    /// This module handles registration of all WMI-specific providers and converters.
    /// It serves as the single entry point for all WMI functionality in the PropertyGrid,
    /// enabling clean separation of WMI-specific code from the generic PropertyGrid implementation.
    /// </summary>
    public static class ProviderModule
    {
        private static bool _isRegistered = false;

        /// <summary>
        /// Registers all WMI-specific providers and converters with the registry.
        /// This method should be called during application initialization.
        /// </summary>
        public static void RegisterBaseWmiProviders()
        {
            if (_isRegistered)
                return;

            try
            {
                var registry = PropertyTypeProviderRegistry.Instance;

                // Register WMI-specific provider
                registry.RegisterProvider(new BaseWmiPropertyTypeProvider());

                // Register WMI-specific value converter
                registry.RegisterConverter(new WmiPropertyValueConverter());

                _isRegistered = true;

                System.Diagnostics.Debug.WriteLine("Base WMI providers and converters registered successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering Base WMI providers: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Registers the WmiInstancePropertyTypeProvider for WmiInstance support.
        /// </summary>
        public static void RegisterWmiInstanceProvider()
        {
            var registry = PropertyTypeProviderRegistry.Instance;
            // Adjust the namespace if needed
            registry.RegisterProvider(new WmiExplorer.Presentation.PropertyTypeProvider.WmiInstancePropertyTypeProvider());
            System.Diagnostics.Debug.WriteLine("WmiInstancePropertyTypeProvider registered");
        }

        /// <summary>
        /// Unregisters WMI providers (for testing or cleanup purposes).
        /// Not typically needed in production code.
        /// </summary>
        public static void UnregisterBaseWmiProviders()
        {
            _isRegistered = false;

            // In the future, if we implement unregister functionality in the registry,
            // we would call it here. Currently, the registry doesn't support removing
            // registered providers.
        }
    }
}