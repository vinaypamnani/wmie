using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Common.Messages;

#region classes

/// <summary>
/// Message sent when selected class changes
/// </summary>
public class SelectedClassChangedMessage : MessageBase
{
    public SelectedClassChangedMessage(WmiClassViewModel classViewModel)
    {
        ClassViewModel = classViewModel;
    }

    public WmiClassViewModel ClassViewModel { get; }
}

/// <summary>
/// Message sent when selected WMI event changes
/// </summary>
public class SelectedEventChangedMessage : MessageBase
{
    public SelectedEventChangedMessage(WmiEvent? wmiEvent)
    {
        WmiEvent = wmiEvent;
    }

    public WmiEvent? WmiEvent { get; }
}

/// <summary>
/// Message sent when selected instance changes
/// </summary>
public class SelectedInstanceChangedMessage : MessageBase
{
    public SelectedInstanceChangedMessage(WmiInstanceViewModel instanceViewModel)
    {
        InstanceViewModel = instanceViewModel;
    }

    public WmiInstanceViewModel InstanceViewModel { get; }
}

/// <summary>
/// Message sent when selected namespace changes
/// </summary>
public class SelectedNamespaceChangedMessage : MessageBase
{
    public SelectedNamespaceChangedMessage(WmiNamespaceViewModel namespaceViewModel)
    {
        NamespaceViewModel = namespaceViewModel;
    }

    public WmiNamespaceViewModel NamespaceViewModel { get; }
}

/// <summary>
/// Message sent when selected search result changes
/// </summary>
public class SelectedSearchResultChangedMessage : MessageBase
{
    public SelectedSearchResultChangedMessage(WmiSearchResult? selectedResult)
    {
        SelectedResult = selectedResult;
    }

    public WmiSearchResult? SelectedResult { get; }
}

/// <summary>
/// Message sent when selected WMI query result instance changes
/// </summary>
public class WmiQueryInstanceChangedMessage : MessageBase
{
    public WmiQueryInstanceChangedMessage(WmiInstance? instance)
    {
        Instance = instance;
    }

    public WmiInstance? Instance { get; }
}

#endregion