using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services
{
    public interface ISettingsService
    {
        // Events for property changes
        event EventHandler<WmiClassTypeFlags> ClassTypeFilterChanged;

        event EventHandler<string> ThemeChanged;

        // Properties
        WmiClassTypeFlags ClassTypeFilter { get; set; }

        string CurrentTheme { get; set; }

        // Main window position and size as a single property
        MainWindowPosition MainWindowPosition { get; set; }

        void ReloadSettings();

        // Methods
        void SaveSettings();
    }
}