using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Integration.AvalonEdit.Context;
using WmiExplorer.Integration.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.AvalonEdit.Providers;

/// <summary>
/// Provides value completions (NULL, TRUE, FALSE) after comparison operators.
/// </summary>
internal class ValueCompletionProvider : ICompletionProvider
{
    public bool CanProvideCompletion(QueryContext context)
    {
        // Only check for AfterOperator context type
        return context.ContextType == QueryContext.ContextKind.AfterOperator;
    }

    /// <summary>
    /// Gets completion data for value suggestions after an operator.
    /// </summary>
    /// <param name="context">The query context.</param>
    /// <param name="prefix">The current input prefix.</param>
    /// <param name="cacheService">The cache service for property lookup. Must not be null.</param>
    /// <param name="namespacePath">The WMI namespace path.</param>
    /// <returns>List of completion data.</returns>
    public async Task<List<ICompletionData>> GetCompletionDataAsync(
        QueryContext context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath)
    {
        var completions = new List<ICompletionData>();

        // Only provide completions if context is AfterOperator
        if (context.ContextType == QueryContext.ContextKind.AfterOperator)
        {
            // Only add NULL values if last significant token is 'IS'
            if ((context.LastSignificantToken?.Text ?? string.Empty).Equals(WqlKeywordManager.Is, StringComparison.OrdinalIgnoreCase))
            {
                AddNullKeywords(completions, prefix);
            }

            // Add boolean values if property is bool
            if (!string.IsNullOrEmpty(context.OperatorProperty)
                && cacheService != null
                && !string.IsNullOrEmpty(context.ClassName)
                && !string.IsNullOrEmpty(namespacePath))
            {
                try
                {
                    var cachedProperties = await cacheService.GetPropertiesForClassAsync(namespacePath, context.ClassName);

                    var property = cachedProperties.FirstOrDefault(p => p.Name.Equals(context.OperatorProperty, StringComparison.OrdinalIgnoreCase));
                    if (property != null && property.Type.Equals("boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        AddBoolKeywords(completions, prefix);
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle cache lookup errors gracefully
                    System.Diagnostics.Debug.WriteLine($"[ValueCompletionProvider] Cache lookup error: {ex.Message}");
                }
            }
        }

        return completions;
    }

    private static void AddBoolKeywords(List<ICompletionData> completions, string prefix)
    {
        // Add boolean values TRUE/FALSE
        foreach (var boolValue in WqlKeywordManager.GetBoolValues())
        {
            if (string.IsNullOrEmpty(prefix) ||
                boolValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new WqlCompletionData(
                    boolValue,
                    CompletionType.Special,
                    $"Boolean value: {boolValue}"));
            }
        }
    }

    private static void AddNullKeywords(List<ICompletionData> completions, string prefix)
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
    }
}