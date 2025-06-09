namespace WmiExplorer.Integration.AvalonEdit.WqlManager;

/// <summary>
/// Enhanced token types for improved parsing and completion.
/// </summary>
public enum WqlTokenType
{
    Unknown,
    Keyword,
    Identifier,
    QuotedIdentifier,
    QuotedValue,
    Operator,
    String,
    Number,
    Whitespace,
    OpenParenthesis,     // New
    CloseParenthesis,    // New
}

/// <summary>
/// Enhanced WQL token with additional metadata.
/// </summary>
internal class WqlToken
{
    public WqlToken(WqlTokenType type, string text, int startIndex)
    {
        Type = type;
        Text = text;
        StartIndex = startIndex;
    }

    public WqlTokenType Type { get; }
    public string Text { get; }
    public int StartIndex { get; }
    public int EndIndex => StartIndex + Text.Length;

    public override string ToString() => $"({Type}) '{Text}' @{StartIndex}";

    // Implicit conversion to string for easier use in LINQ expressions
    public static implicit operator string(WqlToken token) => token.Text;
}
