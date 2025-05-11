using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels;

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
    /// Base class for all messages in the application
    /// </summary>
    public abstract class MessageBase
    { }

    /// <summary>
    /// Message sent when selected class changes
    /// </summary>
    public class SelectedClassChangedMessage : MessageBase
    {
        public SelectedClassChangedMessage(WmiClassesViewModel classViewModel)
        {
            ClassViewModel = classViewModel;
        }

        public WmiClassesViewModel ClassViewModel { get; }
    }

    /// <summary>
    /// Message sent when selected instance changes
    /// </summary>
    public class SelectedInstanceChangedMessage : MessageBase
    {
        public SelectedInstanceChangedMessage(WmiInstancesViewModel instanceViewModel)
        {
            InstanceViewModel = instanceViewModel;
        }

        public WmiInstancesViewModel InstanceViewModel { get; }
    }

    /// <summary>
    /// Message sent when selected namespace changes
    /// </summary>
    public class SelectedNamespaceChangedMessage : MessageBase
    {
        public SelectedNamespaceChangedMessage(WmiNamespacesViewModel namespaceViewModel)
        {
            NamespaceViewModel = namespaceViewModel;
        }

        public WmiNamespacesViewModel NamespaceViewModel { get; }
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
}