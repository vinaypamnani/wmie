using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using WmiExplorer.Presentation.Views.Dialogs;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the MOF Viewer Dialog.
/// </summary>
public partial class MofViewerDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string mofContent;

    public MofViewerDialogViewModel(string mofContent)
    {
        this.mofContent = mofContent;
    }

    [RelayCommand]
    private void Copy()
    {
        try
        {
            Clipboard.SetText(MofContent);
        }
        catch (System.Exception ex)
        {
            MessageBoxDialog.Show($"Failed to copy MOF to clipboard.\n{ex.Message}", "Copy Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
        }
    }
}