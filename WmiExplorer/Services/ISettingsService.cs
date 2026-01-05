using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Models;

namespace WmiExplorer.Services;

public interface ISettingsService
{
    /// <summary>
    /// Auto-update related settings.
    /// </summary>
    AutoUpdateSettings AutoUpdateSettings { get; set; }
    /// <summary>
    /// Gets the cache expiration time as a TimeSpan.
    /// </summary>
    TimeSpan CacheExpiration { get; }
    /// <summary>
    /// Cache expiration time in days. Cache entries expire after this duration.
    /// </summary>
    double CacheExpirationDays { get; set; }
    /// <summary>
    /// Gets the cache prune interval as a TimeSpan.
    /// </summary>
    TimeSpan CachePruneInterval { get; }
    /// <summary>
    /// Cache prune interval in days. Expired cache entries are permanently removed after this duration.
    /// </summary>
    double CachePruneIntervalDays { get; set; }
    /// <summary>
    /// Class filter for WMI class enumeration.
    /// </summary>
    WmiClassEnumerationFlags ClassEnumerationFilter { get; set; }
    /// <summary>
    /// Configuration Manager related settings.
    /// </summary>
    ConfigMgrSettings ConfigMgrSettings { get; set; }
    /// <summary>
    /// The current theme of the application.
    /// </summary>
    string CurrentTheme { get; set; }
    /// <summary>
    /// The logging level for the application.
    /// </summary>
    LogLevel LogLevel { get; set; }
    /// <summary>
    /// The position and size of the main window.
    /// </summary>
    MainWindowPosition MainWindowPosition { get; set; }
    /// <summary>
    /// The operation mode for WMI operations.
    /// </summary>
    WmiOperationMode OperationMode { get; set; }
    /// <summary>
    /// Indicates if system classes should be shown in the WMI classes list.
    /// </summary>
    bool ShowSystemClasses { get; set; }
    /// <summary>
    /// Controls whether properties in dynamic classes with providers should be treated as writable.
    /// When enabled, properties in classes with both Dynamic and Provider qualifiers will be treated as writable.
    /// When disabled (default), only property-level qualifiers determine writeability.
    /// </summary>
    bool TreatDynamicProviderAsWritable { get; set; }
    /// <summary>
    /// Controls whether properties with the 'read' qualifier should be considered read-only.
    /// When enabled, properties with read=true will be considered read-only.
    /// When disabled (default), the 'read' qualifier is ignored for writeability determination.
    /// </summary>
    bool TreatReadQualifierAsReadOnly { get; set; }

    /// <summary>
    /// Reloads the settings from the persistent storage.
    /// </summary>
    void ReloadSettings();
    /// <summary>
    /// Saves the current settings to the persistent storage (debounced).
    /// </summary>
    void SaveSettings();
    /// <summary>
    /// Immediately saves the current settings to the persistent storage, bypassing debounce logic.
    /// </summary>
    void SaveSettingsImmediate();
}