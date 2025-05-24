namespace WmiExplorer.Presentation.Views.Tabs;

/// <summary>
/// Interaction logic for WmiSearchTab.xaml
/// </summary>
public partial class WmiSearchTab : System.Windows.Controls.UserControl
{
    public WmiSearchTab()
    {
        InitializeComponent();
        this.Loaded += WmiSearchTab_Loaded;
    }

    private void WmiSearchTab_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Try to set DataContext from MainWindow.DataContext
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow?.DataContext is ViewModels.MainViewModel mainVm)
        {
            DataContext = mainVm.SearchViewModel;
            System.Diagnostics.Debug.WriteLine("[WmiSearchTab] DataContext set to MainViewModel.SearchViewModel");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[WmiSearchTab] WARNING: MainWindow.DataContext is not MainViewModel.");
            // Optionally, set to a new instance for debugging
            // DataContext = new ViewModels.WmiSearchViewModel(...);
        }
        this.Loaded -= WmiSearchTab_Loaded;
    }
}