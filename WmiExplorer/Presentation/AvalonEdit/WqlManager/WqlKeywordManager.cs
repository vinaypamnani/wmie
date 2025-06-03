using System.Collections.ObjectModel;

namespace WmiExplorer.Presentation.AvalonEdit.WqlManager;

/// <summary>
/// Unified WQL keywords management with all categories consolidated.
/// Provides organized access to all WQL keywords and operators.
/// </summary>
internal static class WqlKeywordManager
{
    // Public constants for commonly used keywords (maintaining compatibility)
    public const string Select = "SELECT";
    public const string Star = "*";
    public const string From = "FROM";
    public const string Where = "WHERE";
    public const string And = "AND";
    public const string Or = "OR";
    public const string Not = "NOT";
    public const string Is = "IS";
    public const string Like = "LIKE";
    public const string Null = "NULL";
    public const string NotNull = "NOT NULL";
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

    // Keyword Collections
    public static readonly IReadOnlyList<string> BaseKeywords = new ReadOnlyCollection<string>(new[]
    {
        Select, Star, From, Where, Having, KeysOnly, Class
    });

    public static readonly IReadOnlyList<string> LogicalOperators = new ReadOnlyCollection<string>(new[]
    {
        And, Or, Not
    });

    public static readonly IReadOnlyList<string> ComparisonOperators = new ReadOnlyCollection<string>(new[]
    {
        Eq, Neq1, Neq2, Lt, Gt, Lte, Gte, Is, Like
    });

    public static readonly IReadOnlyList<string> SpecialOperators = new ReadOnlyCollection<string>(new[]
    {
        Isa, Within, AssociatorsOf, ReferencesOf
    });

    public static readonly IReadOnlyList<string> NullValues = new ReadOnlyCollection<string>(new[]
    {
        Null, NotNull
    });

    public static readonly IReadOnlyList<string> BoolValues = new ReadOnlyCollection<string>(new[]
    {
        True, False
    });

    /// <summary>
    /// Keyword metadata structure.
    /// </summary>
    public record struct WqlKeywordInfo(
        string Text,
        bool IsLogical,
        bool IsComparison,
        bool IsOperator,
        bool IsClause,
        bool IsSpecial);

    /// <summary>
    /// Gets all keywords from all categories.
    /// </summary>
    public static IEnumerable<string> AllKeywords
    {
        get
        {
            return BaseKeywords
                .Concat(LogicalOperators)
                .Concat(ComparisonOperators)
                .Concat(SpecialOperators)
                .Concat(NullValues)
                .Concat(BoolValues);
        }
    }

    /// <summary>
    /// Gets keyword information for a given text.
    /// </summary>
    public static WqlKeywordInfo? GetKeywordInfo(string text)
    {
        var isLogical = LogicalOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isComparison = ComparisonOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isSpecial = SpecialOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isClause = BaseKeywords.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isNull = NullValues.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isBool = BoolValues.Contains(text, StringComparer.OrdinalIgnoreCase);

        if (isLogical || isComparison || isSpecial || isClause || isNull || isBool)
        {
            return new WqlKeywordInfo(
                text,
                isLogical,
                isComparison,
                isLogical || isComparison || isSpecial,
                isClause,
                isSpecial || isNull || isBool);
        }

        return null;
    }

    /// <summary>
    /// Gets keywords matching the specified criteria.
    /// </summary>
    public static IEnumerable<string> GetKeywords(Func<WqlKeywordInfo, bool> predicate)
    {
        return AllKeywords.Where(keyword =>
        {
            var info = GetKeywordInfo(keyword);
            return info.HasValue && predicate(info.Value);
        });
    }

    /// <summary>
    /// Checks if a text is a logical operator.
    /// </summary>
    public static bool IsLogicalOperator(string text)
    {
        return LogicalOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a text is a comparison operator.
    /// </summary>
    public static bool IsComparisonOperator(string text)
    {
        return ComparisonOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a text is a special operator.
    /// </summary>
    public static bool IsSpecialOperator(string text)
    {
        return SpecialOperators.Contains(text, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all keywords in a specific category.
    /// </summary>
    public static IEnumerable<string> GetByCategory(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "base" => BaseKeywords,
            "logical" => LogicalOperators,
            "comparison" => ComparisonOperators,
            "special" => SpecialOperators,
            "null" => NullValues,
            "boolean" => BoolValues,
            _ => Enumerable.Empty<string>()
        };
    }

    /// <summary>
    /// Gets base keywords (SELECT, FROM, WHERE, etc.).
    /// </summary>
    public static IEnumerable<string> GetBaseKeywords() => BaseKeywords;

    /// <summary>
    /// Gets logical operators (AND, OR, NOT).
    /// </summary>
    public static IEnumerable<string> GetLogicalOperators() => LogicalOperators;

    /// <summary>
    /// Gets comparison operators (=, !=, <, >, etc.).
    /// </summary>
    public static IEnumerable<string> GetComparisonOperators() => ComparisonOperators;

    /// <summary>
    /// Gets special operators (IS, LIKE, ISA, etc.).
    /// </summary>
    public static IEnumerable<string> GetSpecialOperators() => SpecialOperators;

    /// <summary>
    /// Gets null values (NULL, NOT NULL).
    /// </summary>
    public static IEnumerable<string> GetNullValues() => NullValues;

    /// <summary>
    /// Gets boolean values (TRUE, FALSE).
    /// </summary>
    public static IEnumerable<string> GetBoolValues() => BoolValues;
}
