using WmiExplorer.Common.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Messages;

/// <summary>
/// Message sent when application state changes
/// </summary>
public class ApplicationStateMessage : MessageBase
{
    public ApplicationStateMessage(ApplicationState state)
    {
        State = state;
    }

    public ApplicationState State { get; }
}

/// <summary>
/// Message to request PropertyGrid refresh
/// </summary>
public class PropertyGridRefreshMessage : MessageBase
{
    public PropertyGridRefreshMessage()
    {
    }
}

/// <summary>
/// Unified message sent when any selection changes in the application
/// </summary>
public class SelectionChangedMessage : MessageBase
{
    public SelectionChangedMessage(ISelectionService selectionService)
    {
        SelectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
    }

    public ISelectionService SelectionService { get; }
}

/// <summary>
/// Message sent when theme changes
/// </summary>
public class ThemeChangedMessage : MessageBase
{
    public ThemeChangedMessage(string theme)
    {
        Theme = theme;
    }

    public string Theme { get; }
}