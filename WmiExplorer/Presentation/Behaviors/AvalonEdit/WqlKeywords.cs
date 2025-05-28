namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// Centralized WQL keyword/operator definitions and metadata for completion and parsing.
/// </summary>
internal static class WqlKeywords
{
    // Public constants for all WQL keywords/operators
    public const string Select = "SELECT";
    public const string From = "FROM";
    public const string Where = "WHERE";
    public const string And = "AND";
    public const string Or = "OR";
    public const string Not = "NOT";
    public const string Is = "IS";
    public const string Like = "LIKE";
    public const string Null = "NULL";
    public const string Isa = "ISA";
    public const string Within = "WITHIN";
    public const string AssociatorsOf = "ASSOCIATORS OF";
    public const string ReferencesOf = "REFERENCES OF";
    public const string KeysOnly = "KEYSONLY";
    public const string Having = "HAVING";
    public const string Group = "GROUP";
    public const string Class = "__CLASS";
    public const string True = "TRUE";
    public const string False = "FALSE";

    // Comparison operators
    public const string Eq = "=";
    public const string Neq1 = "!=";
    public const string Neq2 = "<>";
    public const string Lt = "<";
    public const string Gt = ">";
    public const string Lte = "<=";
    public const string Gte = ">=";

    // Struct to hold keyword info and flags
    public record struct WqlKeywordInfo(string Text, bool IsLogical, bool IsComparison, bool IsOperator, bool IsClause, bool IsSpecial);    // Centralized list of all WQL keywords/operators with flags
    public static readonly IReadOnlyList<WqlKeywordInfo> All = new List<WqlKeywordInfo>
        {
            new(Select, false, false, false, true, false),
            new(From, false, false, false, true, false),
            new(Where, false, false, false, true, false),
            new(And, true, false, true, false, false),
            new(Or, true, false, true, false, false),
            new(Not, true, false, true, false, false),
            new(Is, false, true, true, false, false),
            new(Like, false, true, true, false, false),
            new(Null, false, false, false, false, false),
            new(Isa, false, false, true, false, false),
            new(Within, false, false, false, true, false),
            new(AssociatorsOf, false, false, false, true, false),
            new(ReferencesOf, false, false, false, true, false),
            new(KeysOnly, false, false, false, true, false),
            new(Having, false, false, false, true, false),
            new(Group, false, false, false, true, false),
            new(Class, false, false, false, false, true),
            new(True, false, false, false, false, false),
            new(False, false, false, false, false, false),
            // Comparison operators
            new(Eq, false, true, true, false, false),
            new(Neq1, false, true, true, false, false),
            new(Neq2, false, true, true, false, false),
            new(Lt, false, true, true, false, false),
            new(Gt, false, true, true, false, false),
            new(Lte, false, true, true, false, false),
            new(Gte, false, true, true, false, false)
        };

    // Helper: get keywords by flag
    public static IEnumerable<string> GetKeywords(Func<WqlKeywordInfo, bool> predicate) =>
        All.Where(predicate).Select(k => k.Text);

    // Helper: get keyword info by text
    public static WqlKeywordInfo? GetKeywordInfo(string text) =>
        All.FirstOrDefault(k => k.Text.Equals(text, StringComparison.OrdinalIgnoreCase));
}
