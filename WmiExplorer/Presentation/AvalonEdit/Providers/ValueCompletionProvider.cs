using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Provides value completions (NULL, TRUE, FALSE) after comparison operators.
/// </summary>
internal class ValueCompletionProvider : ICompletionProvider
{

    public bool CanProvideCompletion(QueryContext context)
    {
        var lastSignificantTokenText = context.LastSignificantToken?.Text ?? string.Empty;
        return context.ContextType == QueryContext.ContextKind.AfterOperator && lastSignificantTokenText.Equals(WqlKeywordManager.Is, StringComparison.OrdinalIgnoreCase);
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

            if (context.ContextType == QueryContext.ContextKind.AfterOperator)
            {
                AddValueKeywords(completions, prefix);
            }

            return completions;
        });
    }

    private static void AddValueKeywords(List<ICompletionData> completions, string prefix)
    {
        // Add NULL values
        foreach (var nullValue in WqlKeywordManager.GetNullValues())
        {
            if (string.IsNullOrEmpty(prefix) ||
                nullValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new WqlCompletionData(
                    nullValue,
                    CompletionType.Special,
                    $"Null value: {nullValue}"));
            }
        }

        // Add boolean values
        // foreach (var boolValue in WqlKeywordManager.GetBoolValues())
        // {
        //     if (string.IsNullOrEmpty(prefix) ||
        //         boolValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        //     {
        //         completions.Add(new WqlCompletionData(
        //             boolValue,
        //             CompletionType.Special,
        //             $"Boolean value: {boolValue}"));
        //     }
        // }
    }
}