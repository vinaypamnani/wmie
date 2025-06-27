using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly IMessengerService _messengerService;
    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    public MainWindow(
        IMessengerService messengerService,
        ISettingsService settingsService,
        ThemeManager themeManager,
        MainViewModel mainViewModel)
    {
        InitializeComponent();
        Current = this;

        _messengerService = messengerService;
        _settingsService = settingsService;
        _themeManager = themeManager;

        // Create the main view model with injected services
        _mainViewModel = mainViewModel;

        // Set the DataContext for data binding
        DataContext = _mainViewModel;

        // Initialize title bar theming
        InitializeTitleBarTheming();

        // Set an initial application state
        _messengerService.Send(new ApplicationStateMessage(
            ApplicationState.Ready("Application started. Click Connect to begin.")));
    }

    // Static reference to the current main window for global access
    public static MainWindow Current { get; private set; } = null!;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Set the window position and size using the MainWindowPosition property
        var position = _settingsService.MainWindowPosition;
        if (position.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            Left = position.Left;
            Top = position.Top;
            Width = position.Width;
            Height = position.Height;
        }

        // Save the window position and size when it is closed
        Closing += (s, args) =>
        {
            // Update the view model's window position with current window dimensions
            // while preserving the expander states and column widths
            _mainViewModel.WindowPosition.UpdatePosition(
                left: Left,
                top: Top,
                width: Width,
                height: Height
            );

            // Set IsWindowMaximized property based on current window state
            _mainViewModel.WindowPosition.IsWindowMaximized = WindowState == WindowState.Maximized;

            _settingsService.MainWindowPosition = _mainViewModel.WindowPosition;

            // Explicitly save all settings
            _settingsService.SaveSettings();

            // Clean up the view model resources
            _mainViewModel?.Dispose();
        };
    }

    private void ApplyTitleBarTheme(IntPtr hwnd)
    {
        // The DWMWINDOWATTRIBUTE enum and DwmSetWindowAttribute method have been moved to ThemeService
        // Use the ThemeService's implementation
        _themeManager.ApplyTitleBarTheme(hwnd, Background as SolidColorBrush);
    }

    /// <summary>
    /// Initializes title bar theming to match the application theme.
    /// </summary>
    private void InitializeTitleBarTheming()
    {
        // The SourceInitialized event is needed because we need a window handle
        this.SourceInitialized += (s, e) =>
        {
            // Get window handle
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(new HwndSourceHook(WndProc));

            // Apply the current theme to the title bar
            // ApplyTitleBarTheme(handle);

            // Subscribe to theme change messages to update title bar color with strong reference
            // This ensures the window handle is available when theme changes occur
            _messengerService.StrongSubscribe<ThemeChangedMessage>(handle, message =>
            {
                // Update title bar color when theme changes
                ApplyTitleBarTheme(handle);
            });
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Listen for system theme changes (different from our application theme changes)
        if (msg == 0x031A) // WM_THEMECHANGED
        {
            ApplyTitleBarTheme(hwnd);
        }
        return IntPtr.Zero;
    }
}