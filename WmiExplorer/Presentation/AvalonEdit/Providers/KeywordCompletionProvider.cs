using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Diagnostics;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Provides keyword completions (SELECT, FROM, WHERE, AND, OR, etc.).
/// </summary>
internal class KeywordCompletionProvider : ICompletionProvider
{
    public bool CanProvideCompletion(QueryContext context)
    {
        // Add support for operator completions after property names
        return context.ContextType switch
        {
            QueryContext.ContextKind.StartQuery => true,
            QueryContext.ContextKind.AfterSelect => true,
            QueryContext.ContextKind.AfterStar => true,
            QueryContext.ContextKind.AfterClass => true,
            QueryContext.ContextKind.AfterWhere => true,
            QueryContext.ContextKind.AfterCompleteCondition => true,
            QueryContext.ContextKind.AfterLogicalOperator => true,
            QueryContext.ContextKind.AfterOpenParenthesis => true,
            QueryContext.ContextKind.AfterCloseParenthesis => true,
            QueryContext.ContextKind.AfterProperty => true, // merged: provide operators after property
            _ => false
        };
    }

    public async Task<List<ICompletionData>> GetCompletionDataAsync(
        QueryContext context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath)
    {
        return await Task.Run(() =>
        {
            var completions = new List<ICompletionData>();
            var lastSignificantTokenText = context.LastSignificantToken?.Text ?? string.Empty;

            switch (context.ContextType)
            {
                case QueryContext.ContextKind.StartQuery:
                    AddKeywords(completions, new[] { WqlKeywordManager.Select }, prefix);
                    break;

                case QueryContext.ContextKind.AfterSelect:
                    // Only offer * immediately after SELECT keyword
                    if (lastSignificantTokenText.Equals(WqlKeywordManager.Select, StringComparison.OrdinalIgnoreCase))
                    {
                        AddKeywords(completions, new[] { WqlKeywordManager.Star }, prefix);
                    }
                    break;

                case QueryContext.ContextKind.AfterStar:
                    // Offer FROM after *
                    AddKeywords(completions, new[] { WqlKeywordManager.From }, prefix);
                    break;

                case QueryContext.ContextKind.AfterClass:
                    // Offer WHERE after class name
                    AddKeywords(completions, new[] { WqlKeywordManager.Where }, prefix);
                    break;

                case QueryContext.ContextKind.AfterOpenParenthesis:
                case QueryContext.ContextKind.AfterWhere:
                case QueryContext.ContextKind.AfterLogicalOperator:
                    AddKeywords(completions, new[] { WqlKeywordManager.Not }, prefix);
                    break;

                case QueryContext.ContextKind.AfterCloseParenthesis:
                case QueryContext.ContextKind.AfterCompleteCondition:
                    // Offer logical operators after complete conditions
                    AddKeywords(completions, new[] { WqlKeywordManager.And, WqlKeywordManager.Or }, prefix);
                    break;

                case QueryContext.ContextKind.AfterProperty:
                    // Provide operator completions after property names
                    AddComparisonOperators(completions, prefix);
                    break;
            }

            return completions;
        });
    }

    /// <summary>
    /// Adds keyword completions to the list, with type and description.
    /// </summary>
    private void AddKeywords(List<ICompletionData> completions, IEnumerable<string> keywords, string prefix)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                !keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var info = WqlKeywordManager.GetKeywordInfo(keyword);
            bool isComparisonOperator = info?.IsComparison ?? false;
            bool isLogicalOperator = info?.IsLogical ?? false;

            // Handle special case for '*'
            if (keyword.Equals(WqlKeywordManager.Star, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new WqlCompletionData(keyword, CompletionType.Special, $"Select all properties: {keyword}"));
                continue;
            }

            if (isComparisonOperator)
            {
                completions.Add(new WqlCompletionData(
                    keyword,
                    CompletionType.ComparisonOperator,
                    GetOperatorDescription(keyword)));
                continue;
            }

            if (isLogicalOperator)
            {
                completions.Add(new WqlCompletionData(
                    keyword,
                    CompletionType.LogicalOperator,
                    $"Logical Operator: {keyword}"));
                continue;
            }

            // Default to keyword
            completions.Add(new WqlCompletionData(
                keyword,
                CompletionType.Keyword,
                $"Keyword: {keyword}"));
        }
    }

    /// <summary>
    /// Adds operator completions (=, <, >, LIKE, IS, etc.) to the list.
    /// </summary>
    private static void AddComparisonOperators(List<ICompletionData> completions, string prefix)
    {
        // Add comparison operators
        foreach (var op in WqlKeywordManager.GetComparisonOperators())
        {
            if (string.IsNullOrEmpty(prefix) ||
                op.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string description = GetOperatorDescription(op);
                completions.Add(new WqlCompletionData(
                    op,
                    CompletionType.ComparisonOperator,
                    description));
            }
        }
    }

    /// <summary>
    /// Returns a user-friendly description for a WQL operator.
    /// </summary>
    private static string GetOperatorDescription(string op)
    {
        return op switch
        {
            "=" => "Equals operator",
            "<" => "Less than operator",
            ">" => "Greater than operator",
            "<=" => "Less than or equal operator",
            ">=" => "Greater than or equal operator",
            "!=" => "Not equal operator",
            "<>" => "Not equal operator",
            "IS" => "IS operator (for NULL checks)",
            "LIKE" => "LIKE operator (pattern matching)",
            _ => $"Operator: {op}"
        };
    }
}