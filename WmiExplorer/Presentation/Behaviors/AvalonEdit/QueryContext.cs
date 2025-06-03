namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// Represents the context of a WQL query for intelligent code completion.
/// </summary>
internal class QueryContext
{
    public enum ContextKind
    {
        None,
        AfterSelect,
        AfterFrom,
        AfterWhere,
        InPropertyList,
        InWhereClause,
        StartingQuery
    }

    public string ClassName { get; set; } = string.Empty;
    public ContextKind ContextType { get; set; } = ContextKind.None;
    public WqlToken? LastSignificantToken { get; set; }
    public string LastTokenText { get; set; } = string.Empty;
    public string CurrentWord { get; set; } = string.Empty;
}