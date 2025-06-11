using WmiExplorer.Services;

namespace WmiExplorer.Common.Messages;

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