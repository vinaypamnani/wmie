using System.Windows;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

public partial class MessageBoxDialog : Window
{
    public MessageBoxDialog(string message, string? title = null, MessageBoxDialogButton buttons = MessageBoxDialogButton.OK, MessageBoxDialogIcon icon = MessageBoxDialogIcon.None)
    {
        InitializeComponent();
        var vm = new MessageBoxDialogViewModel(message, title, buttons, icon);
        vm.RequestClose += OnRequestClose;
        DataContext = vm;
    }

    public MessageBoxDialogResult Result { get; private set; } = MessageBoxDialogResult.None;

    public static MessageBoxDialogResult Show(string message, string? title = null, MessageBoxDialogButton buttons = MessageBoxDialogButton.OK, MessageBoxDialogIcon icon = MessageBoxDialogIcon.None, Window? owner = null)
    {
        var dlg = new MessageBoxDialog(message, title, buttons, icon);
        if (owner != null)
            dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Result;
    }

    private void OnRequestClose(MessageBoxDialogResult result)
    {
        Result = result;
        DialogResult = true;
        Close();
    }
}