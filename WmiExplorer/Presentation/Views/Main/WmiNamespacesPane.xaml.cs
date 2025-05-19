namespace WmiExplorer.Presentation.Views.Main
{
    /// <summary>
    /// Interaction logic for WmiNamespacesPane.xaml
    /// </summary>
    public partial class WmiNamespacesPane : System.Windows.Controls.UserControl
    {
        public WmiNamespacesPane()
        {
            InitializeComponent();

            // This View will use the DataContext inherited from MainWindow
            // which contains the Namespaces collection and SelectedNamespace property
        }
    }
}