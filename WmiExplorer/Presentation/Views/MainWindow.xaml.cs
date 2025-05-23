using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Windows API enums and methods
    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20, // Windows 10 1809+
        DWMWA_CAPTION_COLOR = 35 // Added in Windows 11
    }

    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly IMessagingService _messagingService;
    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly MainViewModel _viewModel;
    private readonly IWmiService _wmiService;

    public MainWindow()
    {
        InitializeComponent();
        Current = this;

        // Get services from ServiceLocator
        _messagingService = ServiceLocator.Instance.Get<IMessagingService>();
        _settingsService = ServiceLocator.Instance.Get<ISettingsService>();
        _themeManager = ServiceLocator.Instance.Get<ThemeManager>();
        _wmiService = ServiceLocator.Instance.Get<IWmiService>();
        _applicationService = ServiceLocator.Instance.Get<IApplicationService>();
        _cacheService = ServiceLocator.Instance.Get<ICacheService>();

        // Create the main view model with injected services
        _viewModel = new MainViewModel(
            _messagingService,
            _settingsService,
            _themeManager,
            _wmiService,
            _applicationService,
            _cacheService);

        // Set the DataContext for data binding
        DataContext = _viewModel;

        // Initialize title bar theming
        InitializeTitleBarTheming();

        // Set an initial application state
        _messagingService.Publish(new ApplicationStateMessage(
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
            _viewModel.WindowPosition.UpdatePosition(
                left: Left,
                top: Top,
                width: Width,
                height: Height
            );

            // Set IsWindowMaximized property based on current window state
            _viewModel.WindowPosition.IsWindowMaximized = WindowState == WindowState.Maximized;

            // Explicitly save all settings
            _settingsService.SaveSettings();

            // Clean up the view model resources
            _viewModel?.Dispose();
        };
    }

    private void ApplyTitleBarTheme(IntPtr hwnd)
    {
        try
        {
            // Set dark mode for title bar based on current theme
            bool isDarkTheme = _themeManager?.CurrentThemeName == "Dark";
            uint darkModeValue = isDarkTheme ? 1u : 0u;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(uint));

            // Get the background color from the current theme
            if (_themeManager?.CurrentThemeObject?.ThemeColors.TryGetValue("PrimaryBackgroundColor", out Color bgColor) == true)
            {
                // Convert to win32 COLORREF format (BGR)
                uint colorRef = (uint)((bgColor.R) | (bgColor.G << 8) | (bgColor.B << 16));

                // Set title bar color via DwmApi
                DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
            }
            else if (Background is SolidColorBrush fallbackBrush)
            {
                // Fallback to window background if theme color isn't available
                uint colorRef = (uint)((fallbackBrush.Color.R) | (fallbackBrush.Color.G << 8) | (fallbackBrush.Color.B << 16));
                DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref colorRef, sizeof(uint));
            }
        }
        catch
        {
            // Fail silently if API not supported on this Windows version
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, int cbAttribute);

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
            ApplyTitleBarTheme(handle);

            // Subscribe to theme change messages to update title bar color with strong reference
            // This ensures the window handle is available when theme changes occur
            _messagingService.StrongSubscribe<ThemeChangedMessage>(message =>
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