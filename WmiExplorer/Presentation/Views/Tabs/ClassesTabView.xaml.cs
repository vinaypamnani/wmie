namespace WmiExplorer.Presentation.Views.Tabs
{
    /// <summary>
    /// Interaction logic for ClassesTabView.xaml
    /// </summary>
    public partial class ClassesTabView : System.Windows.Controls.UserControl
    {
        public ClassesTabView()
        {
            InitializeComponent();

            // This View will use the DataContext inherited from MainWindow
            // which contains the SelectedNamespace properties needed for the classes list
        }
    }
}