using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels;
using WmiExplorer.Core.Models;

namespace WmiExplorer.Services
{
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
    /// Message sent when class type filter changes
    /// </summary>
    public class ClassTypeFilterChangedMessage : MessageBase
    {
        public ClassTypeFilterChangedMessage(WmiClassTypeFlags classTypeFilter)
        {
            ClassTypeFilter = classTypeFilter;
        }

        public WmiClassTypeFlags ClassTypeFilter { get; }
    }

    /// <summary>
    /// Message sent when classes are loaded in a namespace
    /// </summary>
    public class ClassesLoadedMessage : MessageBase
    {
        public ClassesLoadedMessage(WmiNamespaceViewModel namespaceViewModel)
        {
            NamespaceViewModel = namespaceViewModel;
        }

        public WmiNamespaceViewModel NamespaceViewModel { get; }
    }

    /// <summary>
    /// Base class for all messages in the application
    /// </summary>
    public abstract class MessageBase
    { }

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
    /// Message sent when classes are filtered in a namespace (e.g., quick filter or type filter changes)
    /// </summary>
    public class ClassesFilteredMessage : MessageBase
    {
        public ClassesFilteredMessage(WmiNamespaceViewModel namespaceViewModel)
        {
            NamespaceViewModel = namespaceViewModel;
        }
        public WmiNamespaceViewModel NamespaceViewModel { get; }
    }
}