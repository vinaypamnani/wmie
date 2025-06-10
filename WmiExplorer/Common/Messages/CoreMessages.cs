using WmiExplorer.Common.Models;

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