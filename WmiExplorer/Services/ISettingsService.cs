using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Models;

namespace WmiExplorer.Services;

public interface ISettingsService
{
    WmiClassEnumerationFlags ClassEnumerationFilter { get; set; }
    string CurrentTheme { get; set; }
    MainWindowPosition MainWindowPosition { get; set; }
    bool ShowSystemClasses { get; set; }

    void ReloadSettings();
    void SaveSettings();
}