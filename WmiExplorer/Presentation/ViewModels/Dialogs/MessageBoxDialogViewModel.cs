using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Input;
using WmiExplorer.Common.Base;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

public class MessageBoxDialogViewModel : DisposableObservableObject
{
    public event Action<MessageBoxDialogResult>? RequestClose;

    public MessageBoxDialogViewModel(string message, string? title, MessageBoxDialogButton buttons, MessageBoxDialogIcon icon, bool showCopyButton = true)
    {
        Message = message;
        Title = title ?? "Message Title";
        Buttons = buttons;
        Icon = icon;
        ShowCopyButton = showCopyButton;

        OkCommand = new RelayCommand(() => Close(MessageBoxDialogResult.OK));
        CancelCommand = new RelayCommand(() => Close(MessageBoxDialogResult.Cancel));
        YesCommand = new RelayCommand(() => Close(MessageBoxDialogResult.Yes));
        NoCommand = new RelayCommand(() => Close(MessageBoxDialogResult.No));
        CopyCommand = new RelayCommand(CopyMessage);
    }

    public MessageBoxDialogButton Buttons { get; }
    public ICommand CancelCommand { get; }
    public Visibility CancelVisibility => (Buttons == MessageBoxDialogButton.OKCancel || Buttons == MessageBoxDialogButton.YesNoCancel) ? Visibility.Visible : Visibility.Collapsed;
    public ICommand CopyCommand { get; }
    public Visibility CopyVisibility => ShowCopyButton ? Visibility.Visible : Visibility.Collapsed;
    public MessageBoxDialogIcon Icon { get; }
    public string Message { get; }
    public ICommand NoCommand { get; }
    public Visibility NoVisibility => (Buttons == MessageBoxDialogButton.YesNo || Buttons == MessageBoxDialogButton.YesNoCancel) ? Visibility.Visible : Visibility.Collapsed;
    public ICommand OkCommand { get; }
    public Visibility OkVisibility => (Buttons == MessageBoxDialogButton.OK || Buttons == MessageBoxDialogButton.OKCancel) ? Visibility.Visible : Visibility.Collapsed;
    public MessageBoxDialogResult Result { get; private set; } = MessageBoxDialogResult.None;
    public bool ShowCopyButton { get; }
    public string Title { get; }
    public ICommand YesCommand { get; }
    public Visibility YesVisibility => (Buttons == MessageBoxDialogButton.YesNo || Buttons == MessageBoxDialogButton.YesNoCancel) ? Visibility.Visible : Visibility.Collapsed;

    private void Close(MessageBoxDialogResult result)
    {
        Result = result;
        RequestClose?.Invoke(result);
    }

    private void CopyMessage()
    {
        try
        {
            Clipboard.SetText(Message);
        }
        catch { /* Optionally handle clipboard exceptions */ }
    }
}

public enum MessageBoxDialogButton { OK, OKCancel, YesNo, YesNoCancel }

public enum MessageBoxDialogIcon { None, Information, Warning, Error, Question }

public enum MessageBoxDialogResult { None, OK, Cancel, Yes, No }