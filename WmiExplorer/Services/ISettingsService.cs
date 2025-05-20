using WmiExplorer.Common.Shared;

namespace WmiExplorer.Services
{
    public interface ISettingsService
    {
        // Events for property changes
        event EventHandler<WmiClassTypeFlags> ClassTypeFilterChanged;

        event EventHandler<string> ThemeChanged;

        // Event for ShowSystemClasses property changes
        event EventHandler<bool> ShowSystemClassesChanged;

        // Properties
        WmiClassTypeFlags ClassTypeFilter { get; set; }

        string CurrentTheme { get; set; }

        // ShowSystemClasses property
        bool ShowSystemClasses { get; set; }

        // Main window position and size as a single property
        MainWindowPosition MainWindowPosition { get; set; }

        void ReloadSettings();

        // Methods
        void SaveSettings();
    }
}