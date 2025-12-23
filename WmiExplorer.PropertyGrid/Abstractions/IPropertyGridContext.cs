namespace WmiExplorer.PropertyGrid.Abstractions;

/// <summary>
/// Provides context for property grid operations, such as read-only state.
/// </summary>
public interface IPropertyGridContext
{
    bool IsReadOnly { get; }
}