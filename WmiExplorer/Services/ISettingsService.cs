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
    /// Class filter for WMI class enumeration.
    /// </summary>
    WmiClassEnumerationFlags ClassEnumerationFilter { get; set; }
    /// <summary>
    /// The current theme of the application.
    /// </summary>
    string CurrentTheme { get; set; }
    /// <summary>
    /// Enables or disables ConfigMgr mode.
    /// </summary>
    bool EnableConfigMgrMode { get; set; }
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
    /// Reloads the settings from the persistent storage.
    /// </summary>
    void ReloadSettings();
    /// <summary>
    /// Saves the current settings to the persistent storage.
    /// </summary>
    void SaveSettings();
}