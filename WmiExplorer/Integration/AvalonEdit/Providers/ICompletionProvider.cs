using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Integration.AvalonEdit.Context;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.AvalonEdit.Providers;

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
