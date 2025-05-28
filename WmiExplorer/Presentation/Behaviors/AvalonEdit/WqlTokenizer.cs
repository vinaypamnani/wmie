namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// A simple tokenizer for WQL (WMI Query Language) syntax.
/// </summary>
internal class WqlTokenizer
{
    // Use centralized WqlKeywords for keywords and operators
    private static readonly HashSet<string> _keywords = new(
        WqlKeywords.GetKeywords(k => k.IsClause || k.IsLogical || k.IsSpecial),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _operators = new(
        WqlKeywords.GetKeywords(k => k.IsOperator || k.IsComparison),
        StringComparer.OrdinalIgnoreCase);

    private int _position;

    private static readonly HashSet<char> _punctuation = new()
            {
                ',', '.', '(', ')', '*', '[', ']', '{', '}', ';'
            };

    private readonly string _text;

    public WqlTokenizer(string text)
    {
        _text = text ?? string.Empty;
        _position = 0;
    }

    public IEnumerable<WqlToken> Tokenize()
    {
        while (_position < _text.Length)
        {
            char currentChar = _text[_position];

            // Handle whitespace
            if (char.IsWhiteSpace(currentChar))
            {
                yield return ReadWhitespace();
                continue;
            }

            // Handle punctuation
            if (_punctuation.Contains(currentChar))
            {
                yield return ReadPunctuation();
                continue;
            }

            // Handle comments (WQL supports -- style comments)
            if (currentChar == '-' && _position + 1 < _text.Length && _text[_position + 1] == '-')
            {
                yield return ReadComment();
                continue;
            }

            // Handle quoted identifiers (backtick or square brackets)
            if (currentChar == '`' || currentChar == '[')
            {
                var quotedIdentifierToken = ReadQuotedIdentifier(currentChar);
                if (quotedIdentifierToken != null)
                {
                    yield return quotedIdentifierToken;
                    continue;
                }
            }

            // Handle quoted values (single or double quotes)
            if (currentChar == '\'' || currentChar == '"')
            {
                var quotedValueToken = ReadQuotedValue(currentChar);
                if (quotedValueToken != null)
                {
                    yield return quotedValueToken;
                    continue;
                }
            }

            // Handle string literals (single quotes)
            if (currentChar == '\'')
            {
                var stringToken = ReadString();
                if (stringToken != null)
                {
                    yield return stringToken;
                    continue;
                }
            }

            // Handle multi-character operators
            var operatorToken = TryReadOperator();
            if (operatorToken != null)
            {
                yield return operatorToken;
                continue;
            }

            // Handle identifiers or keywords
            if (char.IsLetter(currentChar) || currentChar == '_' || currentChar == '\\')
            {
                yield return ReadIdentifierOrKeyword();
                continue;
            }

            // Handle numbers
            if (char.IsDigit(currentChar) || (currentChar == '.' && _position + 1 < _text.Length && char.IsDigit(_text[_position + 1])))
            {
                yield return ReadNumber();
                continue;
            }

            // If none of the above, treat as unknown
            yield return ReadUnknown();
        }
    }

    private WqlToken ReadComment()
    {
        int start = _position;
        _position += 2; // Skip the -- prefix

        // Read until end of line or end of input
        while (_position < _text.Length && _text[_position] != '\n')
        {
            _position++;
        }

        // Include the newline character if present
        if (_position < _text.Length && _text[_position] == '\n')
        {
            _position++;
        }

        return new WqlToken(WqlTokenType.Comment, _text.Substring(start, _position - start), start);
    }

    private WqlToken ReadIdentifierOrKeyword()
    {
        int start = _position;

        // First character already validated
        _position++;

        // Consume the rest of the identifier
        while (_position < _text.Length &&
              (char.IsLetterOrDigit(_text[_position]) ||
               _text[_position] == '_' ||
               _text[_position] == '\\' ||
               _text[_position] == ':' ||  // For namespace references
               _text[_position] == '.'))   // For property access
        {
            _position++;
        }

        string text = _text.Substring(start, _position - start);

        // Check if it's a keyword (case-insensitive)
        if (_keywords.Contains(text))
        {
            return new WqlToken(WqlTokenType.Keyword, text, start);
        }

        return new WqlToken(WqlTokenType.Identifier, text, start);
    }

    private WqlToken ReadNumber()
    {
        int start = _position;

        // Handle optional sign
        if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
        {
            _position++;
        }

        // Read digits before decimal point
        while (_position < _text.Length && char.IsDigit(_text[_position]))
        {
            _position++;
        }

        // Handle decimal point and digits after it
        if (_position < _text.Length && _text[_position] == '.')
        {
            _position++;

            // Read digits after decimal point
            while (_position < _text.Length && char.IsDigit(_text[_position]))
            {
                _position++;
            }
        }

        // Handle scientific notation (e.g., 1.23E+4)
        if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
        {
            _position++;

            // Handle optional sign in exponent
            if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-'))
            {
                _position++;
            }

            // Read exponent digits
            while (_position < _text.Length && char.IsDigit(_text[_position]))
            {
                _position++;
            }
        }

        return new WqlToken(WqlTokenType.Number, _text.Substring(start, _position - start), start);
    }

    private WqlToken ReadPunctuation()
    {
        int start = _position;
        _position++; // Punctuation is always a single character
        return new WqlToken(WqlTokenType.Punctuation, _text.Substring(start, 1), start);
    }

    private WqlToken? ReadQuotedIdentifier(char quoteChar)
    {
        int start = _position;
        _position++; // Consume the opening quote character

        char closingChar = quoteChar == '[' ? ']' : quoteChar;

        while (_position < _text.Length)
        {
            if (_text[_position] == closingChar)
            {
                // Found closing quote
                _position++;
                return new WqlToken(WqlTokenType.QuotedIdentifier, _text.Substring(start, _position - start), start);
            }

            // Handle escaped quotes
            if (_text[_position] == '\\' && _position + 1 < _text.Length && _text[_position + 1] == closingChar)
            {
                _position += 2; // Skip the escape sequence
                continue;
            }

            _position++; // Consume regular character
        }

        // If loop finishes without finding closing quote, emit NonCode token
        var text = _text.Substring(start, _text.Length - start);
        _position = _text.Length;
        return new WqlToken(WqlTokenType.NonCode, text, start);
    }

    private WqlToken? ReadQuotedValue(char quoteChar)
    {
        int start = _position;
        _position++; // Consume the opening quote

        while (_position < _text.Length)
        {
            if (_text[_position] == quoteChar)
            {
                // Handle doubled quotes for escaping (e.g., '' or "")
                if (_position + 1 < _text.Length && _text[_position + 1] == quoteChar)
                {
                    _position += 2;
                    continue;
                }
                _position++;
                return new WqlToken(WqlTokenType.QuotedValue, _text.Substring(start, _position - start), start);
            }
            // Handle backslash escapes
            if (_text[_position] == '\\' && _position + 1 < _text.Length)
            {
                _position += 2;
                continue;
            }
            _position++;
        }
        // If loop finishes without finding closing quote, emit NonCode token
        var text = _text.Substring(start, _text.Length - start);
        _position = _text.Length;
        return new WqlToken(WqlTokenType.NonCode, text, start);
    }

    private WqlToken? ReadString()
    {
        int start = _position;
        _position++; // Consume the opening single quote

        while (_position < _text.Length)
        {
            if (_text[_position] == '\'')
            {
                // Check for doubled single quotes (SQL-style escaping: '' -> ')
                if (_position + 1 < _text.Length && _text[_position + 1] == '\'')
                {
                    _position += 2; // Skip both quotes
                    continue;
                }

                // Found closing quote
                _position++;
                return new WqlToken(WqlTokenType.String, _text.Substring(start, _position - start), start);
            }

            // Handle backslash escapes
            if (_text[_position] == '\\' && _position + 1 < _text.Length)
            {
                _position += 2; // Skip the escape sequence
                continue;
            }

            _position++; // Consume regular character
        }

        // If loop finishes without finding closing quote, emit NonCode token
        var text = _text.Substring(start, _text.Length - start);
        _position = _text.Length;
        return new WqlToken(WqlTokenType.NonCode, text, start);
    }

    private WqlToken ReadUnknown()
    {
        int start = _position;
        _position++; // Consume the unknown character
        return new WqlToken(WqlTokenType.Unknown, _text.Substring(start, 1), start);
    }

    private WqlToken ReadWhitespace()
    {
        int start = _position;
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
        return new WqlToken(WqlTokenType.Whitespace, _text.Substring(start, _position - start), start);
    }

    private WqlToken? TryReadOperator()
    {
        // Try multi-character operators first
        for (int length = 3; length >= 1; length--)
        {
            if (_position + length <= _text.Length)
            {
                string potential = _text.Substring(_position, length);
                if (_operators.Contains(potential))
                {
                    int start = _position;
                    _position += length;
                    return new WqlToken(WqlTokenType.Operator, potential, start);
                }
            }
        }

        return null;
    }
}