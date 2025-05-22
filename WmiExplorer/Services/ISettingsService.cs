using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services;

public interface ISettingsService
{
    event EventHandler<WmiClassTypeFlags> ClassTypeFilterChanged;
    event EventHandler<bool> ShowSystemClassesChanged;
    event EventHandler<string> ThemeChanged;

    WmiClassTypeFlags ClassTypeFilter { get; set; }
    string CurrentTheme { get; set; }
    MainWindowPosition MainWindowPosition { get; set; }
    bool ShowSystemClasses { get; set; }

    void ReloadSettings();
    void SaveSettings();
}