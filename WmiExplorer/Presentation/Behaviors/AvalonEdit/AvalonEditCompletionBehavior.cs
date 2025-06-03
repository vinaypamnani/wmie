using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static WmiExplorer.Presentation.Behaviors.AvalonEdit.SimpleCompletionData;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// Provides auto-completion for AvalonEdit using cacheService and WQL keywords via attached property.
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

    // Track typing timing to avoid opening too many completion windows
    internal static readonly ConcurrentDictionary<TextEditor, DateTime> LastCompletionTime = new();

    private static readonly TimeSpan CompletionThrottleTime = TimeSpan.FromMilliseconds(100);
    private static readonly ConcurrentDictionary<TextEditor, CompletionWindow?> CompletionWindows = new();
    private static readonly QueryContextCache ContextCache = new();

    // Attached property accessors for EnableCompletion
    public static bool GetEnableCompletion(TextEditor editor)
    {
        return (bool)editor.GetValue(EnableCompletionProperty);
    }

    // Attached property accessor for CacheService
    public static void SetCacheService(TextEditor editor, ICacheService? value)
    {
        editor.SetValue(CacheServiceProperty, value);
    }

    public static void SetEnableCompletion(TextEditor editor, bool value)
    {
        editor.SetValue(EnableCompletionProperty, value);
    }

    // Attached property accessor for Namespace
    public static void SetNamespace(TextEditor editor, string? value)
    {
        editor.SetValue(NamespaceProperty, value);
    }

    // Helper to find the parent TextEditor for a given TextArea
    internal static TextEditor? FindParentTextEditor(TextArea textArea)
    {
        DependencyObject? parent = textArea;
        while (parent != null && parent is not TextEditor)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as TextEditor;
    }

    internal static async Task ShowCompletionWindow(TextArea textArea, TextEditor editor, bool forceShow)
    {
        int caretOffset = textArea.Caret.Offset;
        var doc = textArea.Document;
        string textUpToCaret = caretOffset > 0 ? doc.GetText(0, caretOffset) : string.Empty;

        // Tokenize up to caret
        var tokenizer = new WqlTokenizer(textUpToCaret);
        var tokens = tokenizer.Tokenize().ToList();
        if (tokens.LastOrDefault()?.Type == WqlTokenType.NonCode)
            return;

        // Get current word at caret
        int wordStart = TextUtilities.GetNextCaretPosition(doc, caretOffset, LogicalDirection.Backward, CaretPositioningMode.WordStart);
        if (wordStart < 0) wordStart = 0;
        string currentWord = doc.GetText(wordStart, caretOffset - wordStart);

        // If we just typed a space, the current word should be empty
        if (tokens.Count > 0 && tokens.Last().Type == WqlTokenType.Whitespace)
        {
            currentWord = string.Empty;
        }

        // Determine if we're after WHERE or operator (for context override)
        bool afterWhereOrOperator = IsAfterWhereOrOperator(tokens);

        // Get or analyze query context
        QueryContext? queryContext = null;
        if (!ContextCache.TryGetContext(textUpToCaret, out queryContext))
        {
            queryContext = AnalyzeQueryContext(tokens, caretOffset, textUpToCaret);
            ContextCache.AddContext(textUpToCaret, queryContext);
        }

        // Update context with current word and force context if needed
        if (queryContext != null)
        {
            if (!string.IsNullOrEmpty(currentWord))
                queryContext.CurrentWord = currentWord;
            else
                queryContext.CurrentWord = string.Empty;

            if (afterWhereOrOperator)
            {
                // Force AfterWhere context if right after WHERE
                if (tokens.Count >= 2 && tokens.Last().Type == WqlTokenType.Whitespace &&
                    tokens[tokens.Count - 2].Type == WqlTokenType.Keyword &&
                    tokens[tokens.Count - 2].Text.Equals(WqlKeywords.Where, StringComparison.OrdinalIgnoreCase))
                {
                    queryContext.ContextType = QueryContext.ContextKind.AfterWhere;
                }
            }
        }

        var cacheService = GetCacheService(editor);
        var namespacePath = GetNamespace(editor);

        // Generate completion data
        List<ICompletionData> completionData = queryContext != null
            ? await GenerateCompletionData(queryContext, cacheService, namespacePath)
            : new List<ICompletionData>();

        // If no suggestions (unless forced), or only one that matches input, close window
        bool noSuggestions = (completionData.Count == 0 && !forceShow) ||
            (completionData.Count == 1 && completionData[0].Text == queryContext?.CurrentWord);
        if (noSuggestions)
        {
            CloseCompletionWindow(editor);
            return;
        }

        // If only one suggestion and it matches the current word, close window
        if (completionData.Count == 1 && !string.IsNullOrEmpty(currentWord) &&
            completionData[0].Text.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
        {
            CloseCompletionWindow(editor);
            return;
        }

        // Show or update completion window
        if (completionData.Count > 0 || forceShow)
        {
            UpdateOrShowCompletionWindow(editor, textArea, completionData, queryContext, currentWord);
        }
        else if (!forceShow)
        {
            CloseCompletionWindow(editor);
        }
    }

    private static async Task AddClassNames(
        List<ICompletionData> data, ICacheService cacheService, string namespacePath, string prefix)
    {
        try
        {
            var nsCache = await cacheService.GetNamespaceCacheAsync(namespacePath);
            if (nsCache != null && nsCache.Classes != null)
            {
                foreach (var classCache in nsCache.Classes)
                {
                    // Exclude event classes
                    if (classCache.IsEventClass)
                        continue;

                    if (string.IsNullOrEmpty(prefix) ||
                        classCache.ClassName.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string description = classCache.IsSystemClass ? "System Class" : "WMI Class";
                        if (classCache.Properties?.Count > 0)
                            description += $" ({classCache.Properties.Count} properties)";
                        data.Add(new SimpleCompletionData(
                            classCache.ClassName,
                            CompletionType.Class,
                            description));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvalonEditCompletion] Error retrieving classes for namespace '{namespacePath}': {ex}");
        }
    }

    private static async Task AddClassProperties(
        List<ICompletionData> data, ICacheService cacheService, string namespacePath,
        string className, string prefix)
    {
        try
        {
            var nsCache = await cacheService.GetNamespaceCacheAsync(namespacePath);
            if (nsCache != null && nsCache.Classes != null)
            {
                var classCache = nsCache.Classes.FirstOrDefault(c =>
                    c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));

                if (classCache != null && classCache.Properties != null)
                {
                    foreach (var prop in classCache.Properties)
                    {
                        if (string.IsNullOrEmpty(prefix) ||
                            prop.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            // Format the property type more nicely
                            string propertyType = FormatPropertyType(prop.Type);

                            data.Add(new SimpleCompletionData(
                                prop.Name,
                                CompletionType.Property,
                                prop.Name,
                                $"Type: {propertyType}"));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvalonEditCompletion] Error retrieving properties for class '{className}' in namespace '{namespacePath}': {ex}");
        }
    }

    private static void AddKeywords(List<ICompletionData> data, IEnumerable<string> keywords, string prefix)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrEmpty(prefix) && !keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var info = WqlKeywords.GetKeywordInfo(keyword);
            bool isOperator = info?.IsOperator ?? false;
            data.Add(new SimpleCompletionData(
                keyword,
                isOperator ? CompletionType.Operator : CompletionType.Keyword,
                isOperator ? $"Operator: {keyword}" : $"Keyword: {keyword}"));
        }
    }

    // Helper to add NULL/NOT NULL completions after IS
    private static void AddNullNotNullKeywords(List<ICompletionData> data, string prefix)
    {
        AddKeywords(data, new[] { WqlKeywords.Null, "NOT NULL" }, prefix);
    }

    // Shared logic for WHERE clause completions (AfterWhere and InWhereClause)
    private static async Task AddWhereClauseCompletions(
        List<ICompletionData> data,
        QueryContext context,
        ICacheService? cacheService,
        string? namespacePath,
        string prefix)
    {
        // If after a quoted value or number, suggest only AND/OR (not NOT)
        bool afterQuotedOrNumberValue = context.LastSignificantToken != null &&
            IsQuotedTokenValue(context.LastSignificantToken);
        if (afterQuotedOrNumberValue)
        {
            // Only offer AND/OR (not NOT) after a quoted value or number
            var logicalKeywords = WqlKeywords.GetKeywords(k => k.IsLogical && !k.Text.Equals(WqlKeywords.Not, StringComparison.OrdinalIgnoreCase));
            AddKeywords(data, logicalKeywords, prefix);
            return;
        }

        // Special handling: After IS, only suggest NULL/NOT NULL and nothing else
        if (context.LastSignificantToken != null &&
            context.LastSignificantToken.Text.Equals(WqlKeywords.Is, StringComparison.OrdinalIgnoreCase))
        {
            AddNullNotNullKeywords(data, prefix);
            return;
        }

        // If after WHERE, suggest property names and NOT as a logical operator
        if (context.LastSignificantToken != null &&
            context.LastSignificantToken.Type == WqlTokenType.Keyword &&
            context.LastSignificantToken.Text.Equals(WqlKeywords.Where, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(context.ClassName) && cacheService != null && !string.IsNullOrEmpty(namespacePath))
            {
                await AddClassProperties(data, cacheService, namespacePath, context.ClassName, prefix);
            }
            // Suggest NOT as a logical operator, filtered by prefix
            AddKeywords(data, new[] { WqlKeywords.Not }, prefix);
            return;
        }

        // If after a logical operator (AND/OR/NOT), suggest property names and NOT (but NOT after quoted value)
        if (context.LastSignificantToken != null &&
            ((context.LastSignificantToken.Type == WqlTokenType.Keyword || context.LastSignificantToken.Type == WqlTokenType.Operator) &&
             (WqlKeywords.GetKeywordInfo(context.LastSignificantToken.Text)?.IsLogical ?? false)))
        {
            if (!string.IsNullOrEmpty(context.ClassName) && cacheService != null && !string.IsNullOrEmpty(namespacePath))
            {
                await AddClassProperties(data, cacheService, namespacePath, context.ClassName, prefix);
            }
            // Suggest NOT as a logical operator, filtered by prefix, and not after quoted value
            var prevToken = context.LastSignificantToken;
            // Only offer NOT if previous token is not a quoted value or number
            if (prevToken == null || !IsQuotedTokenValue(prevToken))
            {
                AddKeywords(data, new[] { WqlKeywords.Not }, prefix);
            }
            return;
        }

        // Otherwise, suggest properties and logical keywords if in InWhereClause
        if (!string.IsNullOrEmpty(context.ClassName) && cacheService != null && !string.IsNullOrEmpty(namespacePath))
        {
            await AddClassProperties(data, cacheService, namespacePath, context.ClassName, prefix);
        }
        if (context.ContextType == QueryContext.ContextKind.InWhereClause)
        {
            AddKeywords(data, WqlKeywords.GetKeywords(k => k.IsLogical), prefix);
        }
    }

    private static QueryContext AnalyzeQueryContext(List<WqlToken> tokens, int caretOffset, string textUpToCaret)
    {
        var context = new QueryContext();

        // If the last token is whitespace, set CurrentWord to empty and LastTokenText to previous token
        if (tokens.Count > 0 && tokens.Last().Type == WqlTokenType.Whitespace)
        {
            context.CurrentWord = string.Empty;
            // Get the last non-whitespace token for LastTokenText
            var lastNonWhitespaceToken = tokens.TakeWhile((_, index) => index < tokens.Count - 1)
                .LastOrDefault(t => t.Type != WqlTokenType.Whitespace);
            context.LastTokenText = lastNonWhitespaceToken?.Text ?? string.Empty;
        }
        else
        {
            // If not ending with whitespace, the current word is part of the last token
            var lastToken = tokens.LastOrDefault();
            if (lastToken != null && lastToken.Type != WqlTokenType.Whitespace)
            {
                context.CurrentWord = lastToken.Text;
                // For LastTokenText, get the previous non-whitespace token
                var previousNonWhitespaceToken = tokens.TakeWhile((_, index) => index < tokens.Count - 1)
                    .LastOrDefault(t => t.Type != WqlTokenType.Whitespace);
                context.LastTokenText = previousNonWhitespaceToken?.Text ?? string.Empty;
            }
            else
            {
                context.CurrentWord = string.Empty;
                context.LastTokenText = lastToken?.Text ?? string.Empty;
            }
        }

        // Find the last significant token (not whitespace or punctuation) before the caret
        // If the last token is whitespace, look before it for the last non-whitespace, non-punctuation token
        int lastIndex = tokens.Count - 1;
        while (lastIndex >= 0 && (tokens[lastIndex].Type == WqlTokenType.Whitespace || tokens[lastIndex].Type == WqlTokenType.Punctuation))
        {
            lastIndex--;
        }

        if (lastIndex >= 0)
        {
            // The tokenizer already assigns QuotedIdentifier and QuotedValue, so just use the token as-is
            context.LastSignificantToken = tokens[lastIndex];
        }

        // Get sequences of keywords and identifiers to determine context
        var keywords = tokens
            .Where(t => t.Type == WqlTokenType.Keyword)
            .Select(t => t.Text.ToUpperInvariant())
            .ToList();

        // Check for specific SQL clauses
        bool hasSelect = keywords.Contains(WqlKeywords.Select.ToUpperInvariant());
        bool hasFrom = keywords.Contains(WqlKeywords.From.ToUpperInvariant());
        bool hasWhere = keywords.Contains(WqlKeywords.Where.ToUpperInvariant());

        // Find the last clause keyword to determine context
        int lastSelectIndex = keywords.LastIndexOf(WqlKeywords.Select.ToUpperInvariant());
        int lastFromIndex = keywords.LastIndexOf(WqlKeywords.From.ToUpperInvariant());
        int lastWhereIndex = keywords.LastIndexOf(WqlKeywords.Where.ToUpperInvariant());

        // If no keywords at all, we're starting a query
        if (!hasSelect && !hasFrom && !hasWhere)
        {
            context.ContextType = QueryContext.ContextKind.StartingQuery;
            return context;
        }

        // If we're after SELECT but before FROM
        if (hasSelect && (!hasFrom || lastSelectIndex > lastFromIndex))
        {
            context.ContextType = QueryContext.ContextKind.AfterSelect;
            // If there's a FROM clause, try to extract the class name
            if (hasFrom)
            {
                context.ClassName = ExtractClassNameAfterFrom(tokens)!;
            }
            // If we have a dot character in the current token, we might be looking for a property
            if (context.LastSignificantToken?.Text.Contains('.') == true)
            {
                context.ContextType = QueryContext.ContextKind.InPropertyList;
            }
            return context;
        }
        // If we're after FROM but before WHERE
        if (hasFrom && (!hasWhere || lastFromIndex > lastWhereIndex))
        {
            context.ContextType = QueryContext.ContextKind.AfterFrom;
            context.ClassName = ExtractClassNameAfterFrom(tokens)!;
            return context;
        }
        // If we're after WHERE
        if (hasWhere)
        {
            // If WHERE is the last keyword, we're directly after the WHERE clause
            if (lastWhereIndex == keywords.Count - 1)
            {
                context.ContextType = QueryContext.ContextKind.AfterWhere;
            }
            else
            {
                // Otherwise we're somewhere in the WHERE clause
                context.ContextType = QueryContext.ContextKind.InWhereClause;
            }
            context.ClassName = ExtractClassNameAfterFrom(tokens)!;
        }
        return context;
    }

    private static void CleanupEditorResources(TextEditor editor)
    {
        // Close any open completion window
        if (CompletionWindows.TryGetValue(editor, out var window) && window != null)
        {
            window.Close();
        }

        // Remove from dictionaries
        CompletionWindows.TryRemove(editor, out _);
        LastCompletionTime.TryRemove(editor, out _);

        // Unregister from theming behavior
        AvalonEditThemingBehavior.UnregisterEditor(editor);
    }

    // Helper to close the completion window for an editor
    private static void CloseCompletionWindow(TextEditor editor)
    {
        if (CompletionWindows.TryGetValue(editor, out var windowToClose) && windowToClose != null)
        {
            _ = editor.Dispatcher.InvokeAsync(() => windowToClose.Close());
            CompletionWindows[editor] = null;
        }
    }

    private static async void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        // If Ctrl+Space is pressed, show completion
        if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (sender is TextArea textArea)
            {
                var editor = FindParentTextEditor(textArea);
                if (editor != null)
                {
                    await ShowCompletionWindow(textArea, editor, true);
                    e.Handled = true;
                }
            }
        }
    }

    private static async void Editor_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextArea textArea || textArea.Document == null)
            return;

        try
        {
            char ch = e.Text.Length == 1 ? e.Text[0] : '\0';
            var editor = FindParentTextEditor(textArea);
            if (editor == null) return;

            int caretOffset = textArea.Caret.Offset;
            string textUpToCaret = caretOffset > 0 ? textArea.Document.GetText(0, caretOffset) : string.Empty;

            // Throttle completion popup except for dot
            var now = DateTime.UtcNow;
            if (LastCompletionTime.TryGetValue(editor, out var lastTime))
            {
                var elapsed = now - lastTime;
                if (elapsed < CompletionThrottleTime && ch != '.')
                    return;
            }
            LastCompletionTime[editor] = now;

            // Tokenize and analyze context
            var tokenizer = new WqlTokenizer(textUpToCaret);
            var tokens = tokenizer.Tokenize().ToList();
            if (tokens.LastOrDefault()?.Type == WqlTokenType.NonCode)
            {
                if (CompletionWindows.TryGetValue(editor, out var windowToClose) && windowToClose != null)
                {
                    _ = editor.Dispatcher.InvokeAsync(() => windowToClose.Close());
                    CompletionWindows[editor] = null;
                }
                return;
            }

            int wordStart = TextUtilities.GetNextCaretPosition(textArea.Document, caretOffset, LogicalDirection.Backward, CaretPositioningMode.WordStart);
            if (wordStart < 0) wordStart = 0;
            string currentWord = textArea.Document.GetText(wordStart, caretOffset - wordStart);

            // Use context analysis for all completion triggers
            var context = AnalyzeQueryContext(tokens, caretOffset, textUpToCaret);
            bool shouldTriggerCompletion = false;

            // Always trigger after space or dot
            if (ch == ' ' || ch == '.')
            {
                shouldTriggerCompletion = true;
            }
            // For letters/underscore, trigger if building a word or context expects it
            else if (char.IsLetter(ch) || ch == '_')
            {
                // If context is AfterFrom, AfterWhere, InWhereClause, AfterSelect, or InPropertyList, trigger immediately
                shouldTriggerCompletion = currentWord.Length >= 2
                    || context.ContextType == QueryContext.ContextKind.AfterFrom
                    || context.ContextType == QueryContext.ContextKind.AfterWhere
                    || context.ContextType == QueryContext.ContextKind.InWhereClause
                    || context.ContextType == QueryContext.ContextKind.AfterSelect
                    || context.ContextType == QueryContext.ContextKind.InPropertyList;
            }

            // Special case: after SELECT/ FROM + space, handled by context
            if (!shouldTriggerCompletion &&
                (context.ContextType == QueryContext.ContextKind.AfterFrom ||
                 context.ContextType == QueryContext.ContextKind.AfterSelect) && ch == ' ')
            {
                shouldTriggerCompletion = true;
            }

            if (shouldTriggerCompletion)
            {
                await ShowCompletionWindow(textArea, editor, false);
            }
            else
            {
                // If completion is open and we're typing a word, update selection
                if (CompletionWindows.TryGetValue(editor, out var windowToClose) && windowToClose != null)
                {
                    if (char.IsLetterOrDigit(ch) && windowToClose.CompletionList.CompletionData.Count > 0)
                    {
                        windowToClose.CompletionList.SelectItem(currentWord);
                        return;
                    }
                    else
                    {
                        _ = editor.Dispatcher.InvokeAsync(() => windowToClose.Close());
                        CompletionWindows[editor] = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvalonEditCompletion] Unexpected error in Editor_TextEntered: {ex}");
        }
    }

    private static void Editor_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextEditor editor)
        {
            // Clean up resources
            CleanupEditorResources(editor);
        }
    }

    private static string? ExtractClassNameAfterFrom(List<WqlToken> tokens)
    {
        bool foundFrom = false;
        foreach (var token in tokens)
        {
            if (foundFrom && token.Type == WqlTokenType.Identifier)
            {
                // Only trim if the token is a quoted identifier
                if (token.Type == WqlTokenType.QuotedIdentifier)
                    return token.Text.Trim('`', '[', ']', '\'', '"');
                return token.Text;
            }
            if (token.Type == WqlTokenType.Keyword && token.Text.Equals(WqlKeywords.From, StringComparison.OrdinalIgnoreCase))
            {
                foundFrom = true;
            }
        }
        return null;
    }

    // Helper to format WMI property types into more readable formats
    private static string FormatPropertyType(string wmiType)
    {
        return wmiType switch
        {
            "uint8" => "Byte",
            "sint8" => "SByte",
            "uint16" => "UInt16",
            "sint16" => "Int16",
            "uint32" => "UInt32",
            "sint32" => "Int32",
            "uint64" => "UInt64",
            "sint64" => "Int64",
            "real32" => "Single",
            "real64" => "Double",
            "boolean" => "Boolean",
            "string" => "String",
            "datetime" => "DateTime",
            "reference" => "Reference",
            "char16" => "Char",
            "object" => "Object",
            _ => wmiType
        };
    }

    private static async Task<List<ICompletionData>> GenerateCompletionData(
        QueryContext context, ICacheService? cacheService, string? namespacePath)
    {        // Log the context for debugging
        System.Diagnostics.Debug.WriteLine(
                $"[AvalonEditCompletion] Context: {context.ContextType}, " +
                $"LastToken: '{context.LastTokenText}', " +
                $"Prefix: '{context.CurrentWord}', " +
                $"LastSignificantToken: '{context.LastSignificantToken?.Text}', " +
                $"LastSignificantTokenType: '{context.LastSignificantToken?.Type}', " +
                $"Class: '{context.ClassName}'");

        var data = new List<ICompletionData>();
        string prefix = context.CurrentWord;

        // Don't provide completions inside quoted strings
        if (context.LastSignificantToken != null &&
            context.LastSignificantToken.Type == WqlTokenType.String)
            return data;

        // Don't provide completions for quote characters
        if (!string.IsNullOrEmpty(context.LastTokenText) && IsQuoteCharacter(context.LastTokenText.Substring(0, 1)))
            return data;

        switch (context.ContextType)
        {
            case QueryContext.ContextKind.StartingQuery:
                AddKeywords(data, WqlKeywords.GetKeywords(k => k.Text == WqlKeywords.Select), prefix);
                break;
            case QueryContext.ContextKind.AfterSelect:
                // Only offer * immediately after SELECT keyword or after SELECT with space
                if (string.IsNullOrWhiteSpace(context.LastTokenText) || context.LastTokenText.Equals(WqlKeywords.Select, StringComparison.OrdinalIgnoreCase))
                {
                    data.Add(new SimpleCompletionData("*", CompletionType.Special, "Select all properties"));
                }

                // Add class properties
                if (!string.IsNullOrEmpty(context.ClassName) && cacheService != null && !string.IsNullOrEmpty(namespacePath))
                {
                    await AddClassProperties(data, cacheService, namespacePath, context.ClassName, prefix);
                }

                // Only offer FROM if prefix is empty, starts with "FROM", or we just typed "*"
                bool shouldOfferFrom = string.IsNullOrEmpty(context.LastTokenText) ||
                    WqlKeywords.From.StartsWith(context.LastTokenText, StringComparison.OrdinalIgnoreCase) ||
                    (context.LastTokenText.Trim() == "*" && context.LastSignificantToken?.Type != WqlTokenType.QuotedValue);

                if (shouldOfferFrom)
                {
                    AddKeywords(data, WqlKeywords.GetKeywords(k => k.Text == WqlKeywords.From), prefix);
                }
                break;
                
            case QueryContext.ContextKind.AfterFrom:
                var lastToken = context.LastSignificantToken;
                bool justAfterClassName = lastToken != null &&
                    (lastToken.Type == WqlTokenType.Identifier || lastToken.Type == WqlTokenType.QuotedIdentifier) &&
                    string.IsNullOrEmpty(prefix);
                if (!justAfterClassName && cacheService != null && !string.IsNullOrEmpty(namespacePath))
                {
                    var classPrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix;
                    await AddClassNames(data, cacheService, namespacePath, classPrefix);
                }
                if (string.IsNullOrEmpty(prefix) || WqlKeywords.Where.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    AddKeywords(data, WqlKeywords.GetKeywords(k => k.Text == WqlKeywords.Where), prefix);
                }
                break;
            case QueryContext.ContextKind.AfterWhere:
            case QueryContext.ContextKind.InWhereClause:
                var lastSigToken = context.LastSignificantToken;
                bool justAfterProperty = lastSigToken != null &&
                    (lastSigToken.Type == WqlTokenType.Identifier || lastSigToken.Type == WqlTokenType.QuotedIdentifier) &&
                    string.IsNullOrEmpty(prefix);
                bool justAfterComparisonOperator = lastSigToken != null &&
                    lastSigToken.Type == WqlTokenType.Operator &&
                    (WqlKeywords.GetKeywordInfo(lastSigToken.Text)?.IsComparison ?? false);
                if (justAfterProperty)
                {
                    AddKeywords(data, WqlKeywords.GetKeywords(k => k.IsComparison), prefix);
                    break;
                }
                // Prevent property completions after comparison operator (e.g., after '=')
                if (justAfterComparisonOperator)
                {
                    // Optionally, add value completions here if available
                    break;
                }
                await AddWhereClauseCompletions(data, context, cacheService, namespacePath, prefix);
                break;
            default:
                if (!string.IsNullOrEmpty(prefix))
                {
                    var matchingKeywords = WqlKeywords.GetKeywords(k => true)
                        .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (matchingKeywords.Length > 0)
                    {
                        AddKeywords(data, matchingKeywords, prefix);
                    }
                }
                else
                {
                    AddKeywords(data, WqlKeywords.GetKeywords(k => k.Text == WqlKeywords.Select), prefix);
                }
                break;
        }
        return data;
    }

    // Helper to get the ICacheService attached property from a TextEditor
    private static ICacheService? GetCacheService(TextEditor editor)
    {
        return (ICacheService?)editor.GetValue(CacheServiceProperty);
    }

    // Helper to get the Namespace attached property from a TextEditor
    private static string? GetNamespace(TextEditor editor)
    {
        return (string?)editor.GetValue(NamespaceProperty);
    }

    /// <summary>
    /// Determines if the caret is after a WHERE or operator token, for context override.
    /// </summary>
    private static bool IsAfterWhereOrOperator(List<WqlToken> tokens)
    {
        if (tokens.Count < 2 || tokens[^1].Type != WqlTokenType.Whitespace)
            return false;
        var prevToken = tokens[^2];
        bool isLogicalKeyword = prevToken.Type == WqlTokenType.Keyword &&
            (prevToken.Text.Equals(WqlKeywords.Where, StringComparison.OrdinalIgnoreCase) ||
             (WqlKeywords.GetKeywordInfo(prevToken.Text)?.IsLogical ?? false) ||
             (WqlKeywords.GetKeywordInfo(prevToken.Text)?.IsOperator ?? false));
        return isLogicalKeyword || prevToken.Type == WqlTokenType.Operator;
    }

    // Helper method to check if a string is a quote character
    private static bool IsQuoteCharacter(string text)
    {
        return text == "'" || text == "\"" || text == "`" || text == "[";
    }

    // Helper method to check if a token is a quoted token (identifier or value)
    private static bool IsQuotedToken(WqlToken token)
    {
        return token.Type == WqlTokenType.QuotedIdentifier || token.Type == WqlTokenType.QuotedValue;
    }

    // Helper method to check if a token is a quoted value or number (for value completions)
    private static bool IsQuotedTokenValue(WqlToken? token)
    {
        if (token == null)
            return false;

        return token.Type == WqlTokenType.QuotedValue || token.Type == WqlTokenType.Number;
    }

    private static void OnEnableCompletionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor)
        {
            if ((bool)e.NewValue)
            {
                editor.TextArea.TextEntered += Editor_TextEntered;
                editor.TextArea.KeyDown += Editor_KeyDown;
                editor.Unloaded += Editor_Unloaded;
            }
            else
            {
                editor.TextArea.TextEntered -= Editor_TextEntered;
                editor.TextArea.KeyDown -= Editor_KeyDown;
                editor.Unloaded -= Editor_Unloaded;

                // Clean up resources
                CleanupEditorResources(editor);
            }
        }
    }

    // Helper to preselect the best match in the completion list
    private static void PreselectBestMatch(
        CompletionWindow window,
        List<ICompletionData> data,
        QueryContext? queryContext,
        string currentWord)
    {
        if (data.Count == 0 || queryContext == null)
            return;
        string filterText = !string.IsNullOrEmpty(currentWord) ? currentWord : queryContext.LastTokenText;
        if (string.IsNullOrEmpty(filterText))
            return;
        var bestMatch = data
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.Text.StartsWith(filterText, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
        if (bestMatch != null)
            window.CompletionList.SelectedItem = bestMatch;
    }

    // Helper to update or show the completion window
    private static void UpdateOrShowCompletionWindow(
        TextEditor editor,
        TextArea textArea,
        List<ICompletionData> data,
        QueryContext? queryContext,
        string currentWord)
    {
        if (CompletionWindows.TryGetValue(editor, out var openWindow) && openWindow != null)
        {
            // Update existing window
            openWindow.CompletionList.CompletionData.Clear(); foreach (var item in data)
                openWindow.CompletionList.CompletionData.Add(item);

            PreselectBestMatch(openWindow, data, queryContext, currentWord);
        }
        else
        {
            // Create and show new window
            var completionWindow = new CompletionWindow(textArea);

            // Register with theming behavior for theme support
            AvalonEditThemingBehavior.RegisterCompletionWindow(editor, completionWindow);

            // Add completion data
            foreach (var item in data)
                completionWindow.CompletionList.CompletionData.Add(item);

            CompletionWindows[editor] = completionWindow;
            completionWindow.Closed += (o, args) => { CompletionWindows[editor] = null; };

            PreselectBestMatch(completionWindow, data, queryContext, currentWord);
            completionWindow.Show();
        }
    }
}