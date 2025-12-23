using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using WmiExplorer.Common.Messages;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.AvalonEdit.Behaviors;

/// <summary>
/// Applies theming to AvalonEdit using dynamic brushes from the current theme via attached property.
/// Also manages theming for completion windows.
/// </summary>
public static class AvalonEditThemingBehavior
{
    public static readonly DependencyProperty EnableThemingProperty = DependencyProperty.RegisterAttached(
        "EnableTheming",
        typeof(bool),
        typeof(AvalonEditThemingBehavior),
        new PropertyMetadata(false, OnEnableThemingChanged));

    // Private static field for injected messenger service
    private static IMessengerService? _messengerService;

    // Track active completion windows for theme change support
    private static readonly ConcurrentDictionary<TextEditor, CompletionWindow?> ActiveCompletionWindows = new();

    /// <summary>
    /// Applies appropriate styles to a CompletionWindow from theme resources
    /// </summary>
    public static void ApplyThemeToCompletionWindow(CompletionWindow window, TextEditor editor)
    {
        // Apply styles from resources; let XAML handle all visual properties
        if (editor.TryFindResource("CompletionWindowStyle") is Style completionWindowStyle)
        {
            window.Style = completionWindowStyle;
        }
        if (editor.TryFindResource("CompletionListBoxStyle") is Style completionListBoxStyle)
        {
            window.CompletionList.ListBox.Style = completionListBoxStyle;
        }
        if (editor.TryFindResource("CompletionListBoxItemStyle") is Style completionListBoxItemStyle)
        {
            window.CompletionList.ListBox.ItemContainerStyle = completionListBoxItemStyle;
        }
        if (editor.TryFindResource("CompletionListStyle") is Style completionListStyle)
        {
            window.CompletionList.Style = completionListStyle;
        }
        // No need to set borders, padding, or background/foreground here; XAML styles handle them
    }

    public static bool GetEnableTheming(DependencyObject obj) => (bool)obj.GetValue(EnableThemingProperty);

    /// <summary>
    /// Registers a completion window with the theming system
    /// </summary>
    public static void RegisterCompletionWindow(TextEditor editor, CompletionWindow window)
    {
        if (editor != null && window != null)
        {
            // Register the window for theming updates
            ActiveCompletionWindows[editor] = window;

            // Apply the theme to the window
            ApplyThemeToCompletionWindow(window, editor);

            // Add a closed handler to clean up when the window is closed
            window.Closed += (s, e) => ActiveCompletionWindows[editor] = null;
        }
    }

    public static void SetEnableTheming(DependencyObject obj, bool value) => obj.SetValue(EnableThemingProperty, value);

    /// <summary>
    /// Sets the messenger service for this behavior (for DI).
    /// </summary>
    public static void SetMessengerService(IMessengerService messengerService)
    {
        _messengerService = messengerService;
    }

    /// <summary>
    /// Unregisters the editor and cleans up resources
    /// </summary>
    public static void UnregisterEditor(TextEditor editor)
    {
        if (editor != null)
        {
            ActiveCompletionWindows.TryRemove(editor, out _);

            // Clean up message subscriptions
            _messengerService?.UnsubscribeAll(editor);
        }
    }

    private static void ApplyThemeToTextArea(TextEditor editor)
    {
        var resources = Application.Current.Resources;
        editor.TextArea.SelectionBrush = resources["SecondaryAccentBrush"] as Brush;
        editor.TextArea.Caret.CaretBrush = resources["PrimaryForegroundBrush"] as Brush;
    }

    private static void OnEnableThemingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor && (bool)e.NewValue)
        {
            ApplyThemeToTextArea(editor);

            // Subscribe to theme changed messages using the editor as subscriber key
            _messengerService?.StrongSubscribe<ThemeChangedMessage>(editor, _ =>
            {
                // Reapply theme to this editor
                ApplyThemeToTextArea(editor);

                // Reapply theme to any associated completion window
                if (ActiveCompletionWindows.TryGetValue(editor, out var window) && window != null)
                {
                    ApplyThemeToCompletionWindow(window, editor);
                }
            });
        }
    }
}