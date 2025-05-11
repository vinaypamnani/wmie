namespace WmiExplorer.Presentation.Views
{
    /// <summary>
    /// Interaction logic for WmiClassesView.xaml
    /// </summary>
    public partial class WmiClassesView : System.Windows.Controls.UserControl
    {
        public WmiClassesView()
        {
            InitializeComponent();

            // This View will use the DataContext inherited from MainWindow
            // which contains the SelectedNamespace properties needed for the classes list
        }
    }
}