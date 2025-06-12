using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Common.Messages;

/// <summary>
/// Message sent when classes are filtered in a namespace (e.g., quick filter or type filter changes)
/// </summary>
public class InstancesFilteredMessage : MessageBase
{
    public InstancesFilteredMessage(WmiClassViewModel classViewModel)
    {
        ClassViewModel = classViewModel;
    }

    public WmiClassViewModel ClassViewModel { get; }
}