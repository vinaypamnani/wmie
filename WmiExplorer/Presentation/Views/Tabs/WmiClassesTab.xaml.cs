namespace WmiExplorer.Presentation.Views.Tabs
{
    /// <summary>
    /// Interaction logic for WmiClassesTab.xaml
    /// </summary>
    public partial class WmiClassesTab : System.Windows.Controls.UserControl
    {
        public WmiClassesTab()
        {
            InitializeComponent();

            // This View will use the DataContext inherited from MainWindow
            // which contains the SelectedNamespace properties needed for the classes list
        }
    }
}