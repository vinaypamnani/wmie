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
        return context.ContextType switch
        {
            QueryContext.ContextKind.StartQuery => true,
            QueryContext.ContextKind.AfterSelect => true,
            QueryContext.ContextKind.AfterStar => true,
            QueryContext.ContextKind.AfterClass => true,
            QueryContext.ContextKind.AfterWhere => true,
            QueryContext.ContextKind.AfterNot => false, // Properties are handled by PropertyCompletionProvider
            QueryContext.ContextKind.AfterCompleteCondition => true,
            QueryContext.ContextKind.AfterLogicalOperator => true,
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

                case QueryContext.ContextKind.AfterWhere:
                case QueryContext.ContextKind.AfterLogicalOperator:
                    if (!lastSignificantTokenText.Equals(WqlKeywordManager.Not, StringComparison.OrdinalIgnoreCase))
                    {
                        AddKeywords(completions, new[] { WqlKeywordManager.Not }, prefix);
                    }
                    break;

                case QueryContext.ContextKind.AfterCompleteCondition:
                    // Offer logical operators after complete conditions
                    AddKeywords(completions, new[] { WqlKeywordManager.And, WqlKeywordManager.Or }, prefix);
                    break;
            }

            return completions;
        });
    }

    private void AddKeywords(List<ICompletionData> completions, IEnumerable<string> keywords, string prefix)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                !keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var info = WqlKeywordManager.GetKeywordInfo(keyword);
            bool isOperator = info?.IsOperator ?? false;

            if (keyword.Equals(WqlKeywordManager.Star, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new WqlCompletionData(keyword, CompletionType.Special, $"Select all: {keyword}"));
                continue;
            }

            completions.Add(new WqlCompletionData(
                keyword,
                isOperator ? CompletionType.Operator : CompletionType.Keyword,
                isOperator ? $"Operator: {keyword}" : $"Keyword: {keyword}"));
        }
    }
}