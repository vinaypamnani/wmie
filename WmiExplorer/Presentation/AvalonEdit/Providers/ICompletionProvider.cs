using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Interface for modular completion providers.
/// </summary>
internal interface ICompletionProvider
{

    /// <summary>
    /// Determines if this provider can provide completions for the given context.
    /// </summary>
    bool CanProvideCompletion(QueryContext context);

    /// <summary>
    /// Generates completion data for the given context.
    /// </summary>
    Task<List<ICompletionData>> GetCompletionDataAsync(
        QueryContext context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath);
}
