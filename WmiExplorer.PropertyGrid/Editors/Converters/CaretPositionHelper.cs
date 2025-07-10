using System.Windows.Controls;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

/// <summary>
/// Helper class to preserve and restore TextBox caret position
/// </summary>
public static class CaretPositionHelper
{
    /// <summary>
    /// Preserves caret position while executing an action that might modify the TextBox text
    /// </summary>
    public static void PreserveCaretPosition(TextBox textBox, Action action)
    {
        if (textBox == null || action == null) return;

        // Store current caret position and selection
        int caretIndex = textBox.CaretIndex;
        int selectionStart = textBox.SelectionStart;
        int selectionLength = textBox.SelectionLength;
        string originalText = textBox.Text;

        try
        {
            // Execute the action
            action();

            // Only restore caret position if the text actually changed and the textbox still has focus
            if (textBox.IsFocused && !string.Equals(originalText, textBox.Text, StringComparison.Ordinal))
            {
                RestoreCaretPosition(textBox, caretIndex, selectionStart, selectionLength, originalText.Length);
            }
        }
        catch
        {
            // If anything goes wrong, just restore the original position
            if (textBox.IsFocused)
            {
                RestoreCaretPosition(textBox, caretIndex, selectionStart, selectionLength, originalText.Length);
            }
        }
    }

    /// <summary>
    /// Safely sets TextBox text while preserving caret position
    /// </summary>
    public static void SetTextPreservingCaret(TextBox textBox, string newText)
    {
        if (textBox == null) return;

        PreserveCaretPosition(textBox, () =>
        {
            textBox.Text = newText;
        });
    }

    private static void RestoreCaretPosition(TextBox textBox, int originalCaretIndex, int originalSelectionStart, int originalSelectionLength, int originalTextLength)
    {
        try
        {
            int newTextLength = textBox.Text.Length;

            // Calculate new caret position, ensuring it doesn't exceed the new text length
            int newCaretIndex = Math.Min(originalCaretIndex, newTextLength);

            // If the text got shorter, move caret to the end
            if (newTextLength < originalTextLength && originalCaretIndex >= originalTextLength)
            {
                newCaretIndex = newTextLength;
            }

            // Restore caret position
            textBox.CaretIndex = Math.Max(0, newCaretIndex);

            // Restore selection if it was present and still valid
            if (originalSelectionLength > 0)
            {
                int newSelectionStart = Math.Min(originalSelectionStart, newTextLength);
                int maxSelectionLength = newTextLength - newSelectionStart;
                int newSelectionLength = Math.Min(originalSelectionLength, maxSelectionLength);

                if (newSelectionLength > 0)
                {
                    textBox.SelectionStart = newSelectionStart;
                    textBox.SelectionLength = newSelectionLength;
                }
            }
        }
        catch
        {
            // If restoration fails, just set caret to end
            textBox.CaretIndex = textBox.Text.Length;
        }
    }
}