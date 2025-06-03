using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Provides WMI class name completions after FROM keyword.
/// </summary>
internal class ClassCompletionProvider : ICompletionProvider
{

    public bool CanProvideCompletion(QueryContext context)
    {
        return context.ContextType == QueryContext.ContextKind.AfterFrom;
    }

    public async Task<List<ICompletionData>> GetCompletionDataAsync(
        QueryContext context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath)
    {
        var completions = new List<ICompletionData>();

        if (context.ContextType == QueryContext.ContextKind.AfterFrom &&
            cacheService != null &&
            !string.IsNullOrEmpty(namespacePath))
        {
            await AddClassNames(completions, cacheService, namespacePath, prefix);
        }

        return completions;
    }

    private async Task AddClassNames(
        List<ICompletionData> completions,
        ICacheService cacheService,
        string namespacePath,
        string prefix)
    {
        try
        {
            var nsCache = await cacheService.GetNamespaceCacheAsync(namespacePath);
            if (nsCache?.Classes != null)
            {
                foreach (var classCache in nsCache.Classes)
                {
                    // Exclude event classes (typically not used in SELECT queries)
                    if (classCache.IsEventClass)
                        continue;

                    if (string.IsNullOrEmpty(prefix) ||
                        classCache.ClassName.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string description = BuildClassDescription(classCache);
                        completions.Add(new WqlCompletionData(
                            classCache.ClassName,
                            CompletionType.Class,
                            description));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClassCompletionProvider] Error: {ex.Message}");
        }
    }

    private static string BuildClassDescription(dynamic classCache)
    {
        string description = classCache.IsSystemClass ? "System Class" : "WMI Class";

        if (classCache.Properties?.Count > 0)
            description += $" ({classCache.Properties.Count} properties)";

        return description;
    }
}