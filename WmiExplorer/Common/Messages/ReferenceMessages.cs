using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Common.Messages;

public class ReferenceLoadStateChangedMessage
{
    public string PropertyName { get; }
    public ReferenceValueLoadState State { get; }

    public ReferenceLoadStateChangedMessage(string propertyName, ReferenceValueLoadState state)
    {
        PropertyName = propertyName;
        State = state;
    }
}