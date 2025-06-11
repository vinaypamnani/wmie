using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing application-wide selection state
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Gets the currently selected object for property grid display
    /// </summary>
    object? SelectedObject { get; }

    /// <summary>
    /// Gets the display name of the currently selected object
    /// </summary>
    string SelectedObjectDisplayName { get; }

    /// <summary>
    /// Sets the selected object - service determines type automatically
    /// </summary>
    /// <param name="selectedObject">The object to select</param>
    void SetSelectedObject(object? selectedObject);

    /// <summary>
    /// Clears all selections
    /// </summary>
    void ClearSelections();
}
