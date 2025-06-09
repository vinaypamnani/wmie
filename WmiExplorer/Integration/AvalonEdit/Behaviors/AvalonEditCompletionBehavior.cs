using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WmiExplorer.Integration.AvalonEdit.Context;
using WmiExplorer.Integration.AvalonEdit.Providers;
using WmiExplorer.Integration.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.AvalonEdit.Behaviors;

/// <summary>
/// Provides modular auto-completion for AvalonEdit using a provider-based architecture.
/// This replaces the monolithic completion behavior with a clean, extensible system.
/// </summary>
public static class AvalonEditCompletionBehavior
{
    public static readonly DependencyProperty CacheServiceProperty = DependencyProperty.RegisterAttached(
        "CacheService",
        typeof(ICacheService),
        typeof(AvalonEditCompletionBehavior),
        new PropertyMetadata(null));

    public static readonly DependencyProperty EnableCompletionProperty = DependencyProperty.RegisterAttached(
        "EnableCompletion",
        typeof(bool),
        typeof(AvalonEditCompletionBehavior),
        new PropertyMetadata(false, OnEnableCompletionChanged));

    public static readonly DependencyProperty NamespaceProperty = DependencyProperty.RegisterAttached(
        "Namespace",
        typeof(string),
        typeof(AvalonEditCompletionBehavior),
        new PropertyMetadata(null));

    private static readonly List<ICompletionProvider> CompletionProviders = new()
    {
        new KeywordCompletionProvider(),
        new ClassCompletionProvider(),
        new PropertyCompletionProvider(),
        new ValueCompletionProvider()
    };

    private static readonly TimeSpan CompletionThrottleTime = TimeSpan.FromMilliseconds(150);
    private static readonly ConcurrentDictionary<TextEditor, CompletionWindow?> CompletionWindows = new();

    // Track typing timing to avoid opening too many completion windows
    private static readonly ConcurrentDictionary<TextEditor, DateTime> LastCompletionTime = new();

    public static ICacheService? GetCacheService(TextEditor editor)
        => (ICacheService?)editor.GetValue(CacheServiceProperty);

    public static bool GetEnableCompletion(TextEditor editor)
        => (bool)editor.GetValue(EnableCompletionProperty);

    public static string? GetNamespace(TextEditor editor)
        => (string?)editor.GetValue(NamespaceProperty);

    public static void SetCacheService(TextEditor editor, ICacheService? value)
        => editor.SetValue(CacheServiceProperty, value);

    public static void SetEnableCompletion(TextEditor editor, bool value)
        => editor.SetValue(EnableCompletionProperty, value);

    public static void SetNamespace(TextEditor editor, string? value)
        => editor.SetValue(NamespaceProperty, value);

    private static void CloseCompletionWindow(TextEditor editor)
    {
        if (CompletionWindows.TryRemove(editor, out var completionWindow) &&
            completionWindow != null)
        {
            completionWindow.Close();
            // Note: AvalonEditThemingBehavior cleans up its tracking via the Closed event handler
        }
    }

    private static async Task<List<ICompletionData>> CollectCompletionsFromProviders(
        QueryContext? context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath)
    {
        var allCompletions = new List<ICompletionData>();
        var tasks = new List<Task<(string ProviderName, List<ICompletionData> Data)>>();

        // Run all applicable providers in parallel
        foreach (var provider in CompletionProviders)
        {
            if (context == null || provider.CanProvideCompletion(context))
            {
                tasks.Add(GetCompletionssFromProviders(provider, context, prefix, cacheService, namespacePath));
            }
        }

        // Wait for all providers to complete
        var results = await Task.WhenAll(tasks);

        // Collect results and update metrics
        foreach (var (providerName, data) in results)
        {
            allCompletions.AddRange(data);
        }

        return allCompletions;
    }

    private static TextEditor? FindParentTextEditor(TextArea textArea)
    {
        DependencyObject? parent = textArea;
        while (parent != null && parent is not TextEditor)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as TextEditor;
    }

    private static async Task<(string ProviderName, List<ICompletionData> Data)> GetCompletionssFromProviders(
            ICompletionProvider provider,
            QueryContext? context,
            string prefix,
            ICacheService? cacheService,
            string? namespacePath)
    {

        var providerName = provider.GetType().Name;

        try
        {
            var data = context != null
                ? await provider.GetCompletionDataAsync(context, prefix, cacheService, namespacePath)
                : new List<ICompletionData>();

            return (providerName, data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in {providerName}: {ex.Message}");
            return (providerName, new List<ICompletionData>());
        }
    }

    private static (int WordStart, string Prefix) GetWordAtCursor(TextDocument document, int caretOffset)
    {
        if (caretOffset <= 0 || caretOffset > document.TextLength)
            return (caretOffset, string.Empty);

        // Find word boundaries
        int wordStart = caretOffset;
        while (wordStart > 0)
        {
            char ch = document.GetCharAt(wordStart - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            wordStart--;
        }

        int wordEnd = caretOffset;
        while (wordEnd < document.TextLength)
        {
            char ch = document.GetCharAt(wordEnd);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            wordEnd++;
        }

        string prefix = wordStart < caretOffset
            ? document.GetText(wordStart, caretOffset - wordStart)
            : string.Empty;

        return (wordStart, prefix);
    }

    private static void OnEnableCompletionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
            return;

        bool newValue = (bool)e.NewValue;
        bool oldValue = (bool)e.OldValue;

        if (newValue && !oldValue)
        {
            // Enable completion
            editor.TextArea.TextEntering += OnTextEntering;
            editor.TextArea.TextEntered += OnTextEntered;
            editor.TextArea.KeyDown += OnKeyDown;
        }
        else if (!newValue && oldValue)
        {
            // Disable completion
            editor.TextArea.TextEntering -= OnTextEntering;
            editor.TextArea.TextEntered -= OnTextEntered;
            editor.TextArea.KeyDown -= OnKeyDown;

            // Unregister from theming behavior
            AvalonEditThemingBehavior.UnregisterEditor(editor);

            // Close any open completion window
            CloseCompletionWindow(editor);
        }
    }

    private static async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextArea textArea)
            return;

        var editor = FindParentTextEditor(textArea);
        if (editor == null)
            return;

        // Handle Ctrl+Space for manual completion
        if (e.Key == Key.Space &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            await ShowCompletionWindow(textArea, editor, forceShow: true);
        }
        // Handle Escape to close completion
        else if (e.Key == Key.Escape)
        {
            CloseCompletionWindow(editor);
        }
    }

    private static async void OnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (sender is not TextArea textArea)
            return;

        var editor = FindParentTextEditor(textArea);
        if (editor == null)
            return;

        // Throttle rapid typing
        var requestTime = DateTime.UtcNow;
        LastCompletionTime[editor] = requestTime;

        await Task.Delay(CompletionThrottleTime);

        // Check if user typed again during our delay
        if (LastCompletionTime.TryGetValue(editor, out var lastTime) &&
            lastTime != requestTime)
        {
            // User typed during our delay, so don't show completion
            return;
        }

        // Show completion for certain trigger characters or after sufficient text
        if (ShouldTriggerCompletion(e.Text, textArea))
        {
            await ShowCompletionWindow(textArea, editor, forceShow: false);
        }
    }

    private static void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (sender is not TextArea textArea)
            return;

        var editor = FindParentTextEditor(textArea);
        if (editor == null)
            return;        // Close completion window on certain characters that end completion
        if (CompletionWindows.TryGetValue(editor, out var completionWindow) &&
            completionWindow != null)
        {            // Characters like space, operators that should close completion
            if (e.Text.Length > 0 && WqlTokenizer.SpecialCharacters.Contains(e.Text[0]))
            {
                completionWindow.Close();
            }
        }
    }

    /// <summary>
    /// Preselects the best matching completion item based on current context and prefix.
    /// </summary>
    /// <param name="window">The completion window</param>
    /// <param name="data">Available completion data</param>
    /// <param name="queryContext">Current query context</param>
    /// <param name="currentWord">Current word prefix at cursor</param>
    private static void PreselectBestMatch(
        CompletionWindow window,
        List<ICompletionData> data,
        QueryContext? queryContext,
        string currentWord)
    {
        if (data.Count == 0 || window.CompletionList.CompletionData.Count == 0)
            return;

        // Use current word as filter text, or fallback to last token from context
        string filterText = !string.IsNullOrEmpty(currentWord) ? currentWord :
            (queryContext?.LastTokenText ?? string.Empty);

        // if (string.IsNullOrEmpty(filterText))
        //     return;

        // Find best match with priority:
        // 1. Highest priority
        // 2. Exact prefix match (case insensitive)
        // 3. Contains filter text
        var bestMatch = data
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.Text.StartsWith(filterText, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(d => d.Text.Contains(filterText, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            // Find this item in the completion list and select it
            foreach (var item in window.CompletionList.CompletionData)
            {
                if (item.Text == bestMatch.Text)
                {
                    window.CompletionList.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private static bool ShouldTriggerCompletion(string text, TextArea textArea)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // Trigger on space, newline, carriage return, or opening parenthesis
        if (text == " " || text == "\n" || text == "\r" || text == "(")
            return true;

        // Trigger on letters after sufficient context
        if (char.IsLetter(text[0]))
        {
            var (_, prefix) = GetWordAtCursor(textArea.Document, textArea.Caret.Offset);
            return prefix.Length >= 1; // Show after 1 character
        }

        return false;
    }

    private static async Task ShowCompletionWindow(TextArea textArea, TextEditor editor, bool forceShow)
    {
        try
        {
            var cacheService = GetCacheService(editor);
            var namespacePath = GetNamespace(editor);

            // Analyze current context using the enhanced context analyzer
            int caretOffset = textArea.Caret.Offset;

            string queryText = textArea.Document.Text;
            QueryContext? context;

            if (!QueryContextCache.TryGetContext(queryText, out context))
            {
                context = QueryContext.Analyze(textArea.Document, caretOffset);
                if (context != null)
                {
                    QueryContextCache.AddContext(queryText, context);
                }
            }

            if (context == null && !forceShow)
                return;

            // Get word at cursor for prefix matching
            var (wordStart, prefix) = GetWordAtCursor(textArea.Document, caretOffset);

            // Collect completions from all applicable providers
            var allCompletions = await CollectCompletionsFromProviders(
                context, prefix, cacheService, namespacePath);

            if (allCompletions.Count == 0 && !forceShow)
                return;

            // Check if we already have a completion window open
            var existingWindow = CompletionWindows.TryGetValue(editor, out var window) ? window : null;

            if (existingWindow != null)
            {
                // Update existing window
                existingWindow.StartOffset = wordStart;

                // Clear and repopulate the completion list
                existingWindow.CompletionList.CompletionData.Clear();

                // Add completions sorted by priority then alphabetically
                foreach (var completion in allCompletions.OrderByDescending(c => c.Priority)
                                                        .ThenBy(c => c.Text))
                {
                    existingWindow.CompletionList.CompletionData.Add(completion);
                }

                // Preselect the best matching item
                PreselectBestMatch(existingWindow, allCompletions, context, prefix);

                // Force the window to update its position and size
                existingWindow.InvalidateVisual();
            }
            else
            {
                // Close any existing window that might be in a disposed state
                CloseCompletionWindow(editor);

                // Create and show a new completion window
                var completionWindow = new CompletionWindow(textArea);
                completionWindow.StartOffset = wordStart;

                // Add completions sorted by priority then alphabetically
                foreach (var completion in allCompletions.OrderByDescending(c => c.Priority)
                                                        .ThenBy(c => c.Text))
                {
                    completionWindow.CompletionList.CompletionData.Add(completion);
                }

                // Configure completion behavior
                completionWindow.CompletionList.InsertionRequested += (s, e) =>
                {
                    CompletionWindows.TryRemove(editor, out _);
                };

                completionWindow.Closed += (s, e) =>
                {
                    CompletionWindows.TryRemove(editor, out _);
                    // Note: AvalonEditThemingBehavior already handles cleanup via its own Closed handler
                };

                // Store and show
                CompletionWindows[editor] = completionWindow;

                // Register with theming behavior for theme updates
                AvalonEditThemingBehavior.RegisterCompletionWindow(editor, completionWindow);

                // Preselect the best matching item
                PreselectBestMatch(completionWindow, allCompletions, context, prefix);

                completionWindow.Show();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the editor
            Debug.WriteLine($"Error showing completion window: {ex.Message}");
            CloseCompletionWindow(editor);
        }
    }
}