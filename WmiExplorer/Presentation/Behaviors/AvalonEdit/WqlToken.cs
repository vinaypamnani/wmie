namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// Represents a token in WQL syntax.
/// </summary>
internal class WqlToken
{
    public WqlToken(WqlTokenType type, string text, int startIndex)
    {
        Type = type;
        Text = text;
        StartIndex = startIndex;
    }

    // Get a normalized version of the text for comparisons
    public string NormalizedText => Type == WqlTokenType.Keyword
        ? Text.ToUpperInvariant()
        : Text;

    public int StartIndex { get; }
    public string Text { get; }
    public WqlTokenType Type { get; }

    public override string ToString() => $"({Type}) '{Text}' @{StartIndex}";

    // Implicit conversion to string for easier use in LINQ expressions
    public static implicit operator string(WqlToken token) => token.Text;
}

/// <summary>
/// Defines token types for the WQL tokenizer.
/// </summary>
public enum WqlTokenType
{
    Unknown,
    Keyword,
    Identifier,
    QuotedIdentifier,
    QuotedValue, // Added for distinguishing quoted values in WHERE clause
    Operator,
    String,
    Number,
    Punctuation,
    Whitespace,
    Comment,
    NonCode // Generic non-code context (comments, strings, quoted values, unclosed quotes, etc.)
}