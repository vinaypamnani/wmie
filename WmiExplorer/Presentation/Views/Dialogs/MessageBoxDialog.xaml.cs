using System.Windows;
using WmiExplorer.Presentation.ViewModels.Dialogs;

namespace WmiExplorer.Presentation.Views.Dialogs;

public partial class MessageBoxDialog : Window
{
    public MessageBoxDialog(string message, string? title = null, MessageBoxDialogButton buttons = MessageBoxDialogButton.OK, MessageBoxDialogIcon icon = MessageBoxDialogIcon.None, bool showCopyButton = true)
    {
        InitializeComponent();
        var vm = new MessageBoxDialogViewModel(message, title, buttons, icon, showCopyButton);
        vm.RequestClose += OnRequestClose;
        DataContext = vm;

        SetWindowSizeBasedOnMessage(message);
    }

    public MessageBoxDialogResult Result { get; private set; } = MessageBoxDialogResult.None;

    public static MessageBoxDialogResult Show(string message, string? title = null, MessageBoxDialogButton buttons = MessageBoxDialogButton.OK, MessageBoxDialogIcon icon = MessageBoxDialogIcon.None, Window? owner = null, bool showCopyButton = true)
    {
        var dlg = new MessageBoxDialog(message, title, buttons, icon, showCopyButton);
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

    /// <summary>
    /// Sets the window size based on the message content, accounting for longest line and line count.
    /// Handles exceptions gracefully and falls back to minimum size if needed.
    /// </summary>
    private void SetWindowSizeBasedOnMessage(string message)
    {
        int minWidth = 400;
        int minHeight = 200;
        int maxWidth = 900;
        int maxHeight = 600;

        try
        {
            var lines = message.Split('\n');
            int longestLineLength = lines.Length > 0 ? lines.Max(l => l.Length) : message.Length;
            int lineCount = lines.Length;

            // Estimate width: 8px per character, safeguard max/min
            int estimatedWidth = Math.Min(Math.Max(minWidth, longestLineLength * 8 + 80), maxWidth); // +80 for padding/UI

            // Estimate height: 28px per line, safeguard max/min
            int estimatedHeight = Math.Min(Math.Max(minHeight, lineCount * 28 + 120), maxHeight); // +120 for padding/UI

            Width = estimatedWidth;
            Height = estimatedHeight;
        }
        catch (Exception)
        {
            // Fallback to minimum size and optionally log the error
            Width = minWidth;
            Height = minHeight;
        }
    }
}

public enum MessageBoxDialogButton { OK, OKCancel, YesNo, YesNoCancel }

public enum MessageBoxDialogIcon { None, Information, Warning, Error, Question }

public enum MessageBoxDialogResult { None, OK, Cancel, Yes, No }