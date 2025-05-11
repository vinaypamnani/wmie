namespace WmiExplorer.Presentation.Views
{
    /// <summary>
    /// Interaction logic for WmiNamespacesView.xaml
    /// </summary>
    public partial class WmiNamespacesView : System.Windows.Controls.UserControl
    {
        public WmiNamespacesView()
        {
            InitializeComponent();

            // This View will use the DataContext inherited from MainWindow
            // which contains the Namespaces collection and SelectedNamespace property
        }
    }
}