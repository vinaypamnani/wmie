using System.Windows;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IApplicationService _applicationService;
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

            // Create the main view model with injected services
            _viewModel = new MainViewModel(_messagingService, _settingsService, _themeManager, _wmiService, _applicationService);
            DataContext = _viewModel;

            // Set an initial application state
            _messagingService.Publish(new ApplicationStateMessage(
                ApplicationState.Ready("Application started. Click Connect to begin.")));
        }

        // Static reference to the current main window for global access
        public static MainWindow Current { get; private set; } = null!; // Will be initialized in constructor

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Set the window position and size using the MainWindowPosition property
            var position = _settingsService.MainWindowPosition;
            if (position.Maximized)
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

                // Set Maximized property based on current window state
                _viewModel.WindowPosition.Maximized = WindowState == WindowState.Maximized;

                // Explicitly save all settings
                _settingsService.SaveSettings();

                // Clean up the view model resources
                _viewModel?.Dispose();
            };
        }
    }
}