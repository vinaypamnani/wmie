using System.Windows;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

/// <summary>
/// Interaction logic for MofViewerDialog.xaml
/// </summary>
public partial class MofViewerDialog : Window
{
    public MofViewerDialog(string mofContent)
    {
        InitializeComponent();
        ViewModel = new MofViewerDialogViewModel(mofContent);
        DataContext = ViewModel;
    }

    public MofViewerDialogViewModel ViewModel { get; }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}