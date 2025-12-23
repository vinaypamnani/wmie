namespace WmiExplorer.Presentation.ViewModels.ConfigMgr;

/// <summary>
/// Represents a ConfigMgr Client Action for the context menu.
/// </summary>
public class SmsClientAction
{
    public SmsClientAction(string id, string displayName, string group)
    {
        Id = id;
        DisplayName = displayName;
        Group = group;
    }

    public string DisplayName { get; }
    public string Group { get; }
    public string Id { get; }
}