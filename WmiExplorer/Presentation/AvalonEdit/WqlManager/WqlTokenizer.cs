using System.Text;

namespace WmiExplorer.Presentation.AvalonEdit.WqlManager;

/// <summary>
/// Enhanced WQL tokenizer with parentheses tracking and performance optimizations.
/// </summary>
internal class WqlTokenizer
{
    /// <summary>
    /// Common special characters that represent operators or delimiters in WQL.
    /// </summary>
    public static readonly string SpecialCharacters = " ()[]{}.,;+-*/=<>!&|";

    private int _parenthesesDepth = 0;
    private int _position;
    private readonly string _text;
    private readonly StringBuilder _tokenBuffer = new();
    private readonly List<WqlToken> _tokens = new();

    public WqlTokenizer(string text)
    {
        _text = text ?? string.Empty;
        _position = 0;
    }

    /// <summary>
    /// Calculates the parentheses depth at a given index in the token list.
    /// </summary>
    public static int CalculateParenthesesDepth(List<WqlToken> tokens, int currentIndex)
    {
        int depth = 0;
        for (int i = 0; i <= currentIndex && i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Text == "(")
                depth++;
            else if (token.Text == ")")
                depth = Math.Max(0, depth - 1);
        }
        return depth;
    }

    /// <summary>
    /// Extracts the class name from a query after the FROM keyword.
    /// Enhanced to find class names in various positions and contexts.
    /// </summary>
    public static string? ExtractClassName(List<WqlToken> tokens, int fromIndex)
    {
        if (fromIndex == -1 || fromIndex >= tokens.Count)
            return null;

        // First try to find class name directly after FROM
        if (fromIndex + 1 < tokens.Count)
        {
            var directClassToken = tokens[fromIndex + 1];
            if (directClassToken.Type == WqlTokenType.Identifier)
                return directClassToken.Text;
        }

        // If not found directly, look for the first identifier after FROM (skipping whitespace)
        for (int i = fromIndex + 1; i < tokens.Count; i++)
        {
            if (tokens[i].Type == WqlTokenType.Whitespace)
                continue;

            if (tokens[i].Type == WqlTokenType.Identifier)
                return tokens[i].Text;

            // If we hit another keyword, stop searching
            if (tokens[i].Type == WqlTokenType.Keyword)
                break;
        }

        return null;
    }

    /// <summary>
    /// Finds the index of a keyword in the token list.
    /// </summary>
    public static int FindKeywordIndex(List<WqlToken> tokens, string keyword)
    {
        // First try to find exact keyword matches
        var exactMatch = tokens.FindIndex(t =>
            t.Type == WqlTokenType.Keyword &&
            t.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        if (exactMatch >= 0)
        {
            return exactMatch;
        }

        // If no exact match found, look for potential partial keywords (for autocomplete)
        var partialMatch = tokens.FindIndex(t =>
            t.Type == WqlTokenType.Identifier &&
            keyword.StartsWith(t.Text, StringComparison.OrdinalIgnoreCase) &&
            // Only consider it a match if it's at least 2 characters to avoid false positives
            t.Text.Length >= 2);

        return partialMatch;
    }

    /// <summary>
    /// Finds the last significant (non-whitespace) token before the current index.
    /// </summary>
    public static WqlToken? FindLastSignificantToken(List<WqlToken> tokens, int currentIndex)
    {
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (token.Type != WqlTokenType.Whitespace)
            {
                return token;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the index of a token at or before the given position.
    /// </summary>
    public static int FindTokenAtPosition(List<WqlToken> tokens, int position)
    {
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].StartIndex <= position)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets the partial input at the current token position.
    /// </summary>
    public static string GetPartialInput(List<WqlToken> tokens, int currentIndex)
    {
        if (currentIndex >= 0 && currentIndex < tokens.Count)
        {
            var token = tokens[currentIndex];
            // Return text for identifiers and potential partial keywords
            if (token.Type == WqlTokenType.Identifier)
            {
                return token.Text;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Checks if a complete condition exists before the current position.
    /// A complete condition consists of a property, comparison operator, and value.
    /// </summary>
    /// <param name="tokens">The list of tokens to analyze</param>
    /// <param name="currentIndex">The current token index</param>
    /// <returns>True if a complete condition exists before the current position</returns>
    public static bool HasCompleteConditionBeforeCursor(List<WqlToken> tokens, int currentIndex)
    {
        // Need at least 3 tokens to have a complete condition (property, operator, value)
        if (tokens.Count < 3 || currentIndex < 3)
            return false;

        // Get the token at the current position (if any)
        WqlToken? currentToken = null;
        if (currentIndex < tokens.Count)
        {
            currentToken = tokens[currentIndex];
        }

        // Look back for significant tokens (skipping whitespace)
        List<WqlToken> significantTokens = new();
        int count = 0;

        for (int i = currentIndex - 1; i >= 0 && count < 5; i--)
        {
            if (tokens[i].Type != WqlTokenType.Whitespace)
            {
                significantTokens.Insert(0, tokens[i]);
                count++;
            }
        }

        // If we didn't find enough significant tokens, we don't have a complete condition
        if (significantTokens.Count < 3)
            return false;

        // Check for logical operator followed by a property
        // This indicates we're in a new condition after AND/OR
        if (significantTokens.Count >= 2)
        {
            var lastTwo = significantTokens.TakeLast(2).ToList();
            if (lastTwo.Count == 2 &&
                lastTwo[0].Type == WqlTokenType.Keyword &&
                WqlKeywordManager.IsLogicalOperator(lastTwo[0].Text) &&
                lastTwo[1].Type == WqlTokenType.Identifier)
            {
                // We have a pattern like "... AND PropertyName" so we're not after a complete condition
                return false;
            }
        }

        // First check for "IS NOT NULL" pattern (needs 4 tokens)
        if (significantTokens.Count >= 4)
        {
            // Check for the pattern "Property IS NOT NULL"
            var lastFour = significantTokens.TakeLast(4).ToList();
            if (lastFour.Count == 4 &&
                IsPropertyToken(lastFour[0]) &&
                lastFour[1].Type == WqlTokenType.Operator && lastFour[1].Text.Equals("IS", StringComparison.OrdinalIgnoreCase) &&
                lastFour[2].Type == WqlTokenType.Keyword && lastFour[2].Text.Equals("NOT", StringComparison.OrdinalIgnoreCase) &&
                lastFour[3].Type == WqlTokenType.Keyword && lastFour[3].Text.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check if the last 3 significant tokens form a property-operator-value pattern
        var property = significantTokens[^3];
        var op = significantTokens[^2];
        var value = significantTokens[^1];

        bool isComplete = IsCompleteCondition(property, op, value);

        // Special handling for IS NULL pattern
        if (!isComplete &&
            IsPropertyToken(property) &&
            op.Type == WqlTokenType.Operator &&
            op.Text.Equals("IS", StringComparison.OrdinalIgnoreCase) &&
            value.Type == WqlTokenType.Keyword &&
            value.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            isComplete = true;
        }

        // Special handling for boolean values TRUE/FALSE
        if (!isComplete &&
            IsPropertyToken(property) &&
            IsOperatorToken(op) &&
            value.Type == WqlTokenType.Keyword &&
            (value.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || value.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)))
        {
            isComplete = true;
        }

        return isComplete;
    }

    /// <summary>
    /// Checks if three tokens form a complete property-operator-value condition pattern.
    /// </summary>
    /// <param name="property">The property token.</param>
    /// <param name="op">The operator token.</param>
    /// <param name="value">The value token.</param>
    /// <returns>True if the tokens form a complete condition; otherwise, false.</returns>
    public static bool IsCompleteCondition(WqlToken property, WqlToken op, WqlToken value)
    {
        return IsPropertyToken(property) &&
               IsOperatorToken(op) &&
               IsValueToken(value);
    }

    /// <summary>
    /// Determines if a token represents a valid comparison operator.
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is a valid operator; otherwise, false.</returns>
    public static bool IsOperatorToken(WqlToken? token)
    {
        if (token == null)
            return false;

        return token.Type == WqlTokenType.Operator ||
              (token.Type == WqlTokenType.Keyword &&
               WqlKeywordManager.IsComparisonOperator(token.Text));
    }

    /// <summary>
    /// Determines if a token represents a valid property identifier.
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is a valid property identifier; otherwise, false.</returns>
    public static bool IsPropertyToken(WqlToken? token)
    {
        if (token == null)
            return false;

        return token.Type == WqlTokenType.Identifier ||
               token.Type == WqlTokenType.QuotedIdentifier;
    }

    /// <summary>
    /// Determines if a token represents a valid value in a condition.
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is a valid value; otherwise, false.</returns>
    public static bool IsValueToken(WqlToken? token)
    {
        if (token == null)
            return false;

        return token.Type == WqlTokenType.String ||
               token.Type == WqlTokenType.Number ||
               token.Type == WqlTokenType.QuotedValue ||
               token.Type == WqlTokenType.Identifier;
    }

    /// <summary>
    /// Tokenizes the entire input text.
    /// </summary>
    public List<WqlToken> TokenizeAll()
    {
        _tokens.Clear();
        _position = 0;
        _parenthesesDepth = 0;

        while (_position < _text.Length)
        {
            var token = GetNextToken();
            if (token != null)
            {
                _tokens.Add(token);
            }
        }

        return _tokens;
    }

    /// <summary>
    /// Tokenizes text up to a specific position (for autocompletion scenarios).
    /// </summary>
    /// <param name="text">The text to tokenize</param>
    /// <param name="maxPosition">Optional maximum position to tokenize to</param>
    /// <returns>List of tokens up to the specified position</returns>
    public static List<WqlToken> TokenizeToPosition(string text, int? maxPosition = null)
    {
        if (string.IsNullOrEmpty(text))
            return new List<WqlToken>();

        var tokenizer = new WqlTokenizer(text);
        var tokens = tokenizer.TokenizeAll();

        // If no max position specified, return all tokens
        if (!maxPosition.HasValue)
            return tokens;

        // Filter tokens that start before or at the max position
        return tokens.Where(t => t.StartIndex <= maxPosition.Value).ToList();
    }

    private WqlToken? GetNextToken()
    {
        if (_position >= _text.Length)
            return null;

        char currentChar = _text[_position];
        int startPosition = _position;

        // Skip whitespace but track it
        if (char.IsWhiteSpace(currentChar))
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
            return new WqlToken(WqlTokenType.Whitespace, _text[startPosition.._position], startPosition);
        }

        // Handle parentheses
        if (currentChar == '(')
        {
            _parenthesesDepth++;
            _position++;
            return new WqlToken(WqlTokenType.OpenParenthesis, "(", startPosition);
        }

        if (currentChar == ')')
        {
            _parenthesesDepth = Math.Max(0, _parenthesesDepth - 1);
            _position++;
            return new WqlToken(WqlTokenType.CloseParenthesis, ")", startPosition);
        }

        // Handle quoted strings
        if (currentChar == '\'' || currentChar == '"')
        {
            return ReadQuotedString(currentChar, startPosition);
        }

        // Handle operators
        if (IsOperatorStart(currentChar))
        {
            return ReadOperator(startPosition);
        }

        // Handle numbers
        if (char.IsDigit(currentChar))
        {
            return ReadNumber(startPosition);
        }

        // Handle identifiers and keywords
        if (char.IsLetter(currentChar) || currentChar == '_')
        {
            return ReadIdentifierOrKeyword(startPosition);
        }

        // Handle asterisk separately
        if (currentChar == '*')
        {
            _position++;
            return new WqlToken(WqlTokenType.Keyword, "*", startPosition);
        }

        // Unknown character
        _position++;
        return new WqlToken(WqlTokenType.Unknown, currentChar.ToString(), startPosition);
    }

    private static bool IsMultiCharOperator(string op) =>
        op == "<=" || op == ">=" || op == "!=" || op == "<>";

    private static bool IsOperatorStart(char c)
    {
        // Check if any comparison operator starts with the given character
        return WqlKeywordManager.ComparisonOperators.Any(op => op.Length > 0 && op[0] == c);
    }

    private WqlToken ReadOperator(int startPosition)
    {
        _tokenBuffer.Clear();

        // Try to match the longest possible operator from the current position
        string? matchedOperator = null;
        int maxLength = 0;
        foreach (var op in WqlKeywordManager.ComparisonOperators.OrderByDescending(o => o.Length))
        {
            if (_position + op.Length <= _text.Length &&
                string.Compare(_text, _position, op, 0, op.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                matchedOperator = op;
                maxLength = op.Length;
                break;
            }
        }

        if (matchedOperator != null)
        {
            _position += maxLength;
            return new WqlToken(WqlTokenType.Operator, matchedOperator, startPosition);
        }

        // Fallback: treat as single character unknown operator
        char opChar = _text[_position];
        _position++;
        return new WqlToken(WqlTokenType.Unknown, opChar.ToString(), startPosition);
    }

    private WqlToken ReadIdentifierOrKeyword(int startPosition)
    {
        _tokenBuffer.Clear();

        while (_position < _text.Length &&
               (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
        {
            _tokenBuffer.Append(_text[_position]);
            _position++;
        }
        string tokenText = _tokenBuffer.ToString();
        bool isKeyword = WqlKeywordManager.AllKeywords.Any(k =>
            k.Equals(tokenText, StringComparison.OrdinalIgnoreCase));

        return new WqlToken(
            isKeyword ? WqlTokenType.Keyword : WqlTokenType.Identifier,
            tokenText,
            startPosition);
    }

    private WqlToken ReadNumber(int startPosition)
    {
        _tokenBuffer.Clear();

        while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
        {
            _tokenBuffer.Append(_text[_position]);
            _position++;
        }

        return new WqlToken(WqlTokenType.Number, _tokenBuffer.ToString(), startPosition);
    }

    private WqlToken ReadQuotedString(char quoteChar, int startPosition)
    {
        _tokenBuffer.Clear();
        _position++; // Skip opening quote

        while (_position < _text.Length && _text[_position] != quoteChar)
        {
            _tokenBuffer.Append(_text[_position]);
            _position++;
        }

        if (_position < _text.Length)
            _position++; // Skip closing quote

        return new WqlToken(WqlTokenType.String, _tokenBuffer.ToString(), startPosition);
    }
}