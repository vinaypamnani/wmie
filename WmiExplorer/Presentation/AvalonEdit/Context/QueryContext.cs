using ICSharpCode.AvalonEdit.Document;
using System.Diagnostics;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;

namespace WmiExplorer.Presentation.AvalonEdit.Context;

/// <summary>
/// Simplified query context for WQL autocompletion.
/// Provides essential context information with streamlined analysis.
/// </summary>
internal class QueryContext
{
    public enum ContextKind
    {
        None,                     // Default/unknown state
        StartQuery,               // Beginning - offer SELECT
        AfterSelect,              // After SELECT - offer * or properties
        AfterStar,                // After * - offer FROM
        AfterFrom,                // After FROM - offer class names
        AfterClass,               // After class name - offer WHERE
        AfterWhere,               // After WHERE - offer properties, NOT
        AfterProperty,            // After property - offer operators
        AfterOperator,            // After =, !=, etc. - offer values
        AfterLogicalOperator,     // After AND, OR - offer properties, NOT
        AfterNot,                 // After NOT - offer properties
        AfterCompleteCondition,   // After complete condition - offer AND, OR
        AfterOpenParenthesis,     // After ( - inside condition
        AfterCloseParenthesis,    // After ) - end of condition
        InValue,                  // Inside a value (e.g. string, number)
        SelectProps,              // After SELECT and comma, before FROM - offer property names
    }

    /// <summary>
    /// The WMI class name being queried
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// The current context type that determines what completions to offer
    /// </summary>
    public ContextKind ContextType { get; set; } = ContextKind.None;

    /// <summary>
    /// The last significant token in the query that provides context
    /// </summary>
    public WqlToken? LastSignificantToken { get; set; }

    /// <summary>
    /// The text of the token at the current cursor position
    /// </summary>
    public string LastTokenText { get; set; } = string.Empty;

    /// <summary>
    /// The property name being compared when context is AfterOperator.
    /// </summary>
    public string OperatorProperty { get; set; } = string.Empty;

    /// <summary>
    /// The list of property names already selected in the SELECT clause (for SelectProps context)
    /// </summary>
    public List<string> SelectedProperties { get; set; } = new List<string>();

    /// <summary>
    /// Analyzes the query context at the specified position in the document.
    /// Uses WqlTokenizer methods as the source of truth.
    /// </summary>
    /// <param name="document">The text document containing the query</param>
    /// <param name="caretOffset">Current cursor position</param>
    /// <returns>Analyzed context or null if analysis fails</returns>
    public static QueryContext? Analyze(TextDocument document, int caretOffset)
    {
        try
        {
            // Get text up to cursor
            string text = document.GetText(0, caretOffset);

            // Tokenize up to current position
            var tokens = WqlTokenizer.TokenizeToPosition(text, caretOffset);

            // Create context
            var context = new QueryContext();

            // Determine context based on tokens using WqlTokenizer methods
            context.ContextType = DetermineContextKind(tokens, text);
            context.ClassName = ExtractClassName(document.Text); // Use full query text for class extraction
            context.LastSignificantToken = FindLastSignificantToken(tokens);
            int currentTokenIndex = WqlTokenizer.FindTokenAtPosition(tokens, caretOffset - 1);
            context.LastTokenText = WqlTokenizer.GetPartialInput(tokens, currentTokenIndex);

            // Track selected properties if in SelectProps context
            if (context.ContextType == ContextKind.SelectProps)
            {
                context.SelectedProperties = ExtractSelectedProperties(tokens);
            }

            // Extract property before operator if context is AfterOperator
            if (context.ContextType == ContextKind.AfterOperator)
            {
                context.OperatorProperty = ExtractOperatorProperty(tokens);
            }

            Debug.WriteLine($"[QueryContext] Context: {context.ContextType}, Class: '{context.ClassName}', LastTokenText: '{context.LastTokenText}', LastSignificantToken: '{context.LastSignificantToken?.Text}', LastSignificantTokenType: {context.LastSignificantToken?.Type}, OperatorProperty: '{context.OperatorProperty}'");

            return context;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QueryContext] Error: {ex.Message}");
            return new QueryContext { ContextType = ContextKind.StartQuery };
        }
    }

    /// <summary>
    /// Determines the current context based on the token sequence.
    /// Uses WqlTokenizer methods as the single source of truth.
    /// </summary>
    private static ContextKind DetermineContextKind(List<WqlToken> tokens, string text)
    {
        if (tokens.Count == 0)
            return ContextKind.StartQuery;

        // Get significant tokens (non-whitespace)
        var significantTokens = tokens.Where(t => t.Type != WqlTokenType.Whitespace).ToList();

        if (significantTokens.Count == 0)
            return ContextKind.StartQuery;

        var lastToken = significantTokens.Last();
        int cursorPosition = tokens.Count;

        // Check if cursor is inside a value input (string or number)
        if (IsInValueInput(text, tokens))
        {
            return ContextKind.InValue;
        }

        // AfterOpenParenthesis
        if (lastToken.Type == WqlTokenType.OpenParenthesis)
        {
            return ContextKind.AfterOpenParenthesis;
        }

        // AfterCloseParenthesis
        if (lastToken.Type == WqlTokenType.CloseParenthesis)
        {
            return ContextKind.AfterCloseParenthesis;
        }

        // Use helper for SELECT ... , ... before FROM
        if (IsSelectPropsContext(tokens, significantTokens, lastToken))
            return ContextKind.SelectProps;

        // Check for keywords first (most specific contexts)
        if (lastToken.Type == WqlTokenType.Keyword)
        {
            string keyword = lastToken.Text.ToUpperInvariant();

            switch (keyword)
            {
                case "SELECT": return ContextKind.AfterSelect;
                case "*": return ContextKind.AfterStar;
                case "FROM": return ContextKind.AfterFrom;
                case "WHERE": return ContextKind.AfterWhere;
                case "NOT": return ContextKind.AfterNot;
                case "AND":
                case "OR": return ContextKind.AfterLogicalOperator;
            }
        }

        // Check if last token is a comparison operator
        if (lastToken.Type == WqlTokenType.Operator && WqlKeywordManager.IsComparisonOperator(lastToken.Text))
        {
            return ContextKind.AfterOperator;
        }

        // Check for complete condition before the cursor
        if (WqlTokenizer.HasCompleteConditionBeforeCursor(tokens, cursorPosition))
        {
            return ContextKind.AfterCompleteCondition;
        }

        // Check for context after property (in WHERE clause)
        if (WqlTokenizer.FindKeywordIndex(tokens, "WHERE") >= 0 && lastToken.Type == WqlTokenType.Identifier)
        {
            return ContextKind.AfterProperty;
        }

        // Check for context after class name
        if (WqlTokenizer.FindKeywordIndex(tokens, "FROM") >= 0 && WqlTokenizer.FindKeywordIndex(tokens, "SELECT") >= 0)
        {
            // Find the FROM keyword in significant tokens
            int fromSignificantIndex = significantTokens.FindIndex(
                t => t.Type == WqlTokenType.Keyword &&
                     t.Text.Equals("FROM", StringComparison.OrdinalIgnoreCase));

            if (fromSignificantIndex >= 0 && fromSignificantIndex < significantTokens.Count - 1)
            {
                // If there's at least one token after FROM and it's an identifier, it's a class name
                var classToken = significantTokens[fromSignificantIndex + 1];
                if (classToken.Type == WqlTokenType.Identifier)
                {
                    // If WHERE is present, make sure the class token comes before WHERE
                    if (WqlTokenizer.FindKeywordIndex(tokens, "WHERE") < 0 || tokens.IndexOf(classToken) < WqlTokenizer.FindKeywordIndex(tokens, "WHERE"))
                    {
                        return ContextKind.AfterClass;
                    }
                }
            }
        }

        // If there are no significant tokens or just starting to type, offer SELECT
        if (significantTokens.Count <= 1 &&
            (significantTokens.Count == 0 ||
             (significantTokens[0].Type == WqlTokenType.Identifier &&
              significantTokens[0].Text.Length < 7))) // Could be starting to type "SELECT"
        {
            return ContextKind.StartQuery;
        }

        return ContextKind.None;
    }

    /// <summary>
    /// Extracts the class name from the FROM clause using WqlTokenizer.
    /// This method analyzes the full query text to ensure the class name is found
    /// regardless of caret position.
    /// </summary>
    private static string ExtractClassName(string queryText)
    {
        // Tokenize the entire query text for reliable extraction
        var tokenizer = new WqlTokenizer(queryText);
        var tokens = tokenizer.TokenizeAll();

        int fromIndex = WqlTokenizer.FindKeywordIndex(tokens, "FROM");
        if (fromIndex >= 0)
        {
            string? className = WqlTokenizer.ExtractClassName(tokens, fromIndex);
            return className ?? string.Empty;
        }
        return string.Empty;
    }

    /// <summary>
    /// Extracts the property name before the operator for AfterOperator context.
    /// </summary>
    private static string ExtractOperatorProperty(List<WqlToken> tokens)
    {
        // Find the last comparison operator
        int opIndex = tokens.FindLastIndex(t =>
            t.Type == WqlTokenType.Operator && WqlKeywordManager.IsComparisonOperator(t.Text));

        if (opIndex > 0)
        {
            // Look backwards for the previous identifier (property name)
            for (int i = opIndex - 1; i >= 0; i--)
            {
                if (tokens[i].Type == WqlTokenType.Identifier)
                {
                    return tokens[i].Text;
                }
                // Stop if we hit a keyword or another operator
                if (tokens[i].Type == WqlTokenType.Keyword || tokens[i].Type == WqlTokenType.Operator)
                    break;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extracts property names from the SELECT clause up to FROM.
    /// </summary>
    private static List<string> ExtractSelectedProperties(List<WqlToken> tokens)
    {
        var properties = new List<string>();
        int selectIndex = WqlTokenizer.FindKeywordIndex(tokens, "SELECT");
        int fromIndex = WqlTokenizer.FindKeywordIndex(tokens, "FROM");
        if (selectIndex >= 0)
        {
            int end = fromIndex > selectIndex ? fromIndex : tokens.Count;
            for (int i = selectIndex + 1; i < end; i++)
            {
                var token = tokens[i];
                if (token.Type == WqlTokenType.Identifier)
                {
                    properties.Add(token.Text);
                }
            }
        }
        return properties;
    }

    /// <summary>
    /// Finds the last significant (non-whitespace) token using WqlTokenizer.
    /// </summary>
    private static WqlToken? FindLastSignificantToken(List<WqlToken> tokens)
    {
        if (tokens.Count == 0)
            return null;

        return WqlTokenizer.FindLastSignificantToken(tokens, tokens.Count);
    }

    /// <summary>
    /// Determines if the cursor is currently inside a value input (unclosed string or number being typed).
    /// </summary>
    private static bool IsInValueInput(string text, List<WqlToken> tokens)
    {
        // Check for unclosed string literal
        int singleQuoteCount = text.Count(c => c == '\'');
        int doubleQuoteCount = text.Count(c => c == '"');
        if ((singleQuoteCount % 2 != 0) || (doubleQuoteCount % 2 != 0))
            return true;

        // Check for number being typed
        var lastSignificant = tokens.LastOrDefault(t => t.Type != WqlTokenType.Whitespace);
        if (lastSignificant != null && lastSignificant.Type == WqlTokenType.Number)
        {
            // If the text ends with a digit, treat as in value
            if (text.Length > 0 && char.IsDigit(text[^1]))
                return true;
        }

        return false;
    }

    private static bool IsSelectPropsContext(List<WqlToken> tokens, List<WqlToken> significantTokens, WqlToken lastToken)
    {
        int selectIndex = WqlTokenizer.FindKeywordIndex(tokens, "SELECT");
        int fromIndex = WqlTokenizer.FindKeywordIndex(tokens, "FROM");
        if (selectIndex >= 0 && (fromIndex == -1 || selectIndex < fromIndex))
        {
            int afterSelectIndex = significantTokens.FindIndex(t => t.Type == WqlTokenType.Keyword && t.Text.Equals("SELECT", StringComparison.OrdinalIgnoreCase));
            int fromTokenIndex = fromIndex == -1 ? significantTokens.Count : significantTokens.FindIndex(t => t.Type == WqlTokenType.Keyword && t.Text.Equals("FROM", StringComparison.OrdinalIgnoreCase));
            if (afterSelectIndex >= 0)
            {
                for (int i = afterSelectIndex + 1; i < fromTokenIndex; i++)
                {
                    if (significantTokens[i].Text == ",")
                    {
                        if (lastToken.Text == ",")
                            return true;
                        if (lastToken.Type == WqlTokenType.Identifier && significantTokens.Count > 1 && significantTokens[significantTokens.Count - 2].Text == ",")
                            return true;
                    }
                }
            }
        }
        return false;
    }
}