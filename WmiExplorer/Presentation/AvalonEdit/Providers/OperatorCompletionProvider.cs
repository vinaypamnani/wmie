using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Provides operator completions (=, <, >, LIKE, IS, etc.) after property names.
/// </summary>
internal class OperatorCompletionProvider : ICompletionProvider
{

    public bool CanProvideCompletion(QueryContext context)
    {
        return context.ContextType == QueryContext.ContextKind.AfterProperty;
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

            if (context.ContextType == QueryContext.ContextKind.AfterProperty)
            {
                AddOperators(completions, prefix);
            }

            return completions;
        });
    }

    private static void AddOperators(List<ICompletionData> completions, string prefix)
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
                    CompletionType.Operator,
                    description));
            }
        }
    }

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