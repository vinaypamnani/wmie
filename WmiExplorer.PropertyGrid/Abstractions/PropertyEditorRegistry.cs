namespace WmiExplorer.PropertyGrid.Abstractions;

/// <summary>
/// Registry for property editors that allows registration of specialized editors
/// for different property types or domains.
/// </summary>
public class PropertyEditorRegistry
{
    private readonly List<IPropertyEditor> _editors = new();
    private static readonly Lazy<PropertyEditorRegistry> _instance = new(() => new PropertyEditorRegistry());
    private readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of the property editor registry.
    /// </summary>
    public static PropertyEditorRegistry Instance => _instance.Value;

    /// <summary>
    /// Clears all registered editors.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _editors.Clear();
        }
    }

    /// <summary>
    /// Gets the most appropriate editor for the specified property item.
    /// Returns the first editor that can handle the property.
    /// </summary>
    /// <param name="propertyItem">The property item to find an editor for</param>
    /// <returns>The appropriate editor, or null if no specialized editor is found</returns>
    public IPropertyEditor? GetEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem == null)
            return null;

        lock (_lock)
        {
            return _editors.FirstOrDefault(editor => editor.CanHandle(propertyItem));
        }
    }

    /// <summary>
    /// Registers a property editor with the registry.
    /// </summary>
    /// <param name="editor">The editor to register</param>
    public void RegisterEditor(IPropertyEditor editor)
    {
        lock (_lock)
        {
            if (!_editors.Contains(editor))
            {
                _editors.Add(editor);
            }
        }
    }

    /// <summary>
    /// Unregisters a property editor from the registry.
    /// </summary>
    /// <param name="editor">The editor to unregister</param>
    public void UnregisterEditor(IPropertyEditor editor)
    {
        lock (_lock)
        {
            _editors.Remove(editor);
        }
    }
}