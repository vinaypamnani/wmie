using WmiExplorer.Common.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Common.Messages;

#region classes

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
/// Non-generic version for cases where type isn't known at compile time
/// </summary>
public class SettingChangedMessage : MessageBase
{
    public SettingChangedMessage(string settingName, object? oldValue, object? newValue)
    {
        SettingName = settingName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public object? NewValue { get; }
    public object? OldValue { get; }
    public string SettingName { get; }
}

/// <summary>
/// Generic message sent when any setting changes
/// </summary>
/// <typeparam name="T">The type of the setting value</typeparam>
public class SettingChangedMessage<T> : MessageBase
{
    public T NewValue { get; }
    public T OldValue { get; }
    public string SettingName { get; }

    public SettingChangedMessage(string settingName, T oldValue, T newValue)
    {
        SettingName = settingName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Message sent when any collection count changes that affects tab headers
/// </summary>
public class TabCountChangedMessage : MessageBase
{
    public TabCountChangedMessage()
    {
    }
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

#endregion