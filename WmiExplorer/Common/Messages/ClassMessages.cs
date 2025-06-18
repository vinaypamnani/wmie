using WmiExplorer.Common.Enums;
using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Common.Messages;

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
/// Message sent when class type filter changes
/// </summary>
public class ClassEnumFilterChangedMessage : MessageBase
{
    public ClassEnumFilterChangedMessage(WmiClassEnumerationFlags classTypeFilter)
    {
        ClassTypeFilter = classTypeFilter;
    }

    public WmiClassEnumerationFlags ClassTypeFilter { get; }
}

/// <summary>
/// Message sent to request navigation to a class in a namespace (from search results)
/// </summary>
public class JumpToClassMessage : MessageBase
{
    public JumpToClassMessage(string namespacePath, string className)
    {
        NamespacePath = namespacePath;
        ClassName = className;
    }

    public string ClassName { get; }
    public string NamespacePath { get; }
}